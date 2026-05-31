/** Runtime errors, mirroring PowwowLang.Exceptions. */

/** Thrown deep in evaluation; carries just a message and is wrapped into a
 *  TemplateEvaluationError at a boundary that has context/call-site. */
export class InnerEvaluationError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "InnerEvaluationError";
  }
}

/** A surfaced evaluation failure (type mismatch, missing field, divide-by-zero, etc.). */
export class TemplateEvaluationError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "TemplateEvaluationError";
  }
}

/** A setup-time failure (e.g. unknown template, duplicate function registration). */
export class InitializationError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "InitializationError";
  }
}
