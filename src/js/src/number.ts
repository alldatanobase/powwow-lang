/**
 * PowwowNumber — a faithful port of .NET `System.Decimal` semantics.
 *
 * Powwow's Number type is a .NET `decimal`: a base-10 value with a 96-bit integer
 * mantissa and an explicit scale (number of fractional digits). The two properties
 * that a JavaScript `number` (IEEE-754 double) and even general decimal libraries
 * (decimal.js, big.js) do NOT reproduce, and which we must match exactly for output
 * parity with the .NET interpreter, are:
 *
 *   1. Scale preservation. `2.5 + 1.5` renders "4.0" (not "4"), `10 / 4` renders
 *      "2.5", and `2 + 3` renders "5". The trailing-zero scale is part of the value.
 *   2. Banker's rounding (round-half-to-even). `round(2.5)` is 2 and `round(3.5)`
 *      is 4 — matching .NET `Math.Round` / `Convert.ToInt32`, NOT JS `Math.round`.
 *
 * We model the value as { mantissa: bigint, scale: number >= 0 }, value = mantissa / 10^scale,
 * and replicate .NET's per-operation scale rules. See js-port-parity.md.
 */

const MAX_PRECISION = 28; // .NET decimal supports 28-29 significant digits.
const DIVISION_SCALE = 28; // Working fractional precision for division before trimming.

function pow10(n: number): bigint {
  return 10n ** BigInt(n);
}

function bigAbs(v: bigint): bigint {
  return v < 0n ? -v : v;
}

/** Number of decimal digits in a non-negative bigint. */
function digitCount(v: bigint): number {
  if (v === 0n) return 1;
  return bigAbs(v).toString().length;
}

/**
 * Round `numerator / denominator` to the nearest integer, ties to even.
 * `denominator` must be non-zero; sign of either operand is handled.
 */
function roundHalfEvenDiv(numerator: bigint, denominator: bigint): bigint {
  if (denominator < 0n) {
    numerator = -numerator;
    denominator = -denominator;
  }
  const negative = numerator < 0n;
  const a = bigAbs(numerator);
  let q = a / denominator;
  const r = a % denominator;
  const twice = r * 2n;
  if (twice > denominator || (twice === denominator && q % 2n === 1n)) {
    q += 1n;
  }
  return negative ? -q : q;
}

export class PowwowNumber {
  readonly mantissa: bigint;
  readonly scale: number;

  constructor(mantissa: bigint, scale: number) {
    if (scale < 0) throw new Error("PowwowNumber scale must be >= 0");
    this.mantissa = mantissa;
    this.scale = scale;
  }

  static fromInt(value: number | bigint): PowwowNumber {
    return new PowwowNumber(BigInt(value), 0);
  }

  /** Parse a numeric literal as the lexer/`number()` builtin would (e.g. "2.5", "-.3", "42"). */
  static fromString(text: string): PowwowNumber {
    const trimmed = text.trim();
    const negative = trimmed.startsWith("-");
    const unsigned = negative ? trimmed.slice(1) : trimmed;
    const dot = unsigned.indexOf(".");
    let digits: string;
    let scale: number;
    if (dot === -1) {
      digits = unsigned;
      scale = 0;
    } else {
      const intPart = unsigned.slice(0, dot);
      const fracPart = unsigned.slice(dot + 1);
      digits = intPart + fracPart;
      scale = fracPart.length;
    }
    if (digits === "" || !/^[0-9]+$/.test(digits)) {
      throw new Error(`Cannot convert string '${text}' to number`);
    }
    let mantissa = BigInt(digits);
    if (negative) mantissa = -mantissa;
    return new PowwowNumber(mantissa, scale).capPrecision();
  }

  isZero(): boolean {
    return this.mantissa === 0n;
  }

  /** Re-express at a higher scale (exact) or lower scale (round half-even). */
  rescale(newScale: number): PowwowNumber {
    if (newScale === this.scale) return this;
    if (newScale > this.scale) {
      return new PowwowNumber(this.mantissa * pow10(newScale - this.scale), newScale);
    }
    const factor = pow10(this.scale - newScale);
    return new PowwowNumber(roundHalfEvenDiv(this.mantissa, factor), newScale);
  }

  /** Drop trailing-zero scale, but never below `minScale`. Mirrors .NET's preferred-scale trimming. */
  private trimToScale(minScale: number): PowwowNumber {
    let m = this.mantissa;
    let s = this.scale;
    while (s > minScale && m % 10n === 0n) {
      m /= 10n;
      s -= 1;
    }
    return new PowwowNumber(m, s);
  }

  /** Enforce the 28-29 significant-digit ceiling, rounding away excess fractional digits. */
  private capPrecision(): PowwowNumber {
    const sig = digitCount(this.mantissa);
    if (sig <= MAX_PRECISION) return this;
    const excess = sig - MAX_PRECISION;
    const dropped = Math.min(excess, this.scale); // only fractional digits may be rounded off
    if (dropped <= 0) return this;
    return this.rescale(this.scale - dropped);
  }

  add(other: PowwowNumber): PowwowNumber {
    const s = Math.max(this.scale, other.scale);
    const a = this.mantissa * pow10(s - this.scale);
    const b = other.mantissa * pow10(s - other.scale);
    return new PowwowNumber(a + b, s).capPrecision();
  }

  subtract(other: PowwowNumber): PowwowNumber {
    const s = Math.max(this.scale, other.scale);
    const a = this.mantissa * pow10(s - this.scale);
    const b = other.mantissa * pow10(s - other.scale);
    return new PowwowNumber(a - b, s).capPrecision();
  }

  multiply(other: PowwowNumber): PowwowNumber {
    return new PowwowNumber(this.mantissa * other.mantissa, this.scale + other.scale).capPrecision();
  }

  divide(other: PowwowNumber): PowwowNumber {
    if (other.isZero()) {
      throw new Error("Cannot divide by zero");
    }
    const preferredScale = Math.max(0, this.scale - other.scale);
    // result = (this.m / 10^this.s) / (other.m / 10^other.s), evaluated at DIVISION_SCALE fractional digits.
    const num = this.mantissa * pow10(other.scale + DIVISION_SCALE);
    const den = other.mantissa * pow10(this.scale);
    const q = roundHalfEvenDiv(num, den);
    return new PowwowNumber(q, DIVISION_SCALE).capPrecision().trimToScale(preferredScale);
  }

  /** .NET Convert.ToInt32(decimal): round half-even to an integer. */
  toIntValue(): number {
    return Number(this.rescale(0).mantissa);
  }

  /** Math.Round(value, decimals) — half-even. */
  round(decimals: number): PowwowNumber {
    if (decimals < 0) throw new Error("Number of decimal places cannot be negative");
    return this.rescale(decimals);
  }

  floor(): PowwowNumber {
    if (this.scale === 0) return this;
    const factor = pow10(this.scale);
    let q = this.mantissa / factor;
    if (this.mantissa % factor !== 0n && this.mantissa < 0n) q -= 1n;
    return new PowwowNumber(q, 0);
  }

  ceil(): PowwowNumber {
    if (this.scale === 0) return this;
    const factor = pow10(this.scale);
    let q = this.mantissa / factor;
    if (this.mantissa % factor !== 0n && this.mantissa > 0n) q += 1n;
    return new PowwowNumber(q, 0);
  }

  /** Sign-aware comparison: -1, 0, or 1. */
  compareTo(other: PowwowNumber): number {
    const s = Math.max(this.scale, other.scale);
    const a = this.mantissa * pow10(s - this.scale);
    const b = other.mantissa * pow10(s - other.scale);
    return a < b ? -1 : a > b ? 1 : 0;
  }

  /** Equality by value (scale-insensitive), matching .NET decimal `==`. */
  equals(other: PowwowNumber): boolean {
    return this.compareTo(other) === 0;
  }

  /** Render exactly as .NET `decimal.ToString()` (no exponential notation, scale preserved). */
  toString(): string {
    const negative = this.mantissa < 0n;
    const digits = bigAbs(this.mantissa).toString();
    let body: string;
    if (this.scale === 0) {
      body = digits;
    } else {
      const padded = digits.padStart(this.scale + 1, "0");
      const cut = padded.length - this.scale;
      body = padded.slice(0, cut) + "." + padded.slice(cut);
    }
    return negative && this.mantissa !== 0n ? "-" + body : body;
  }
}
