# Overview

The impetus for this project:
* I needed a fairly expressive language to templatize highly dynamic HTML-based email templates in one of my workflows
* I needed to be able to interpret the code from the templates in .NET Framework assemblies with zero dependencies
  * Some Liquid interpreters fit this bill, but I am not a fan of Liquid, as I find myself wasting time with (my perception of) the idiosyncracies of the language whenever I need to pick it up
  * So I wanted something where the semantics were similar enough to other languages I use daily that, after not having touched it for weeks or even months, I can easily read and be productive with it in minutes
* I needed to be able to bind data from the host language into the interpreter easily
* I wanted to be able to compile new built-in functions alongside the interpreter for often-used functions, or define lambdas in code for one-off things
* I wanted templates to be composable

The result is Powwow Lang, a small templating language. The most obvious sources of inspiration are JavaScript, Handlebars, and Liquid. Some key features:
* Variables
* Conditional statements
* Loops
* Recursion
* Arrays
* JavaScript-style objects (but prototype-less)
* Lambdas
* First-class functions
* Closures
* Template composability

# Implementation note

A .NET interpreter is implemented, written in .NET Framework. My primary use-case is to interpret templates within Microsoft Dataverse plugins, where I compile the interpreter source code directly into existing .NET assemblies with only Microsoft dependencies.

A future goal of the project is to build a modern .NET version of the interpeter with sensible compiler targets.

```
Disclaimer: The current .NET interpreter is quick and dirty. It has no performance goals or optimizations. I use it to compile relatively small email templates where the number of requests is on the order of hundreds per day. This project is in a very early stage, so use at your own risk.
```