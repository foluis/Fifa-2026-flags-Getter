---
description: C# rules
applyTo: "**/*.cs"
---

- Prefer straightforward code, avoid over-engineering.
- Use async/await for I/O.
- Add basic argument validation for public methods.
- Keep logic testable, avoid mixing UI concerns into services.
- Favor composition over inheritance.
- Use expression-bodied members for simple properties and methods.
- Prefer `var` for local variable declarations when the type is obvious.
- Use string interpolation (`$"..."`) instead of `String.Format`.
- Use `using` statements for resource management.
- Follow .NET naming conventions: PascalCase for types and methods, camelCase for parameters and private fields.
- Write XML documentation comments for public APIs.
- Use LINQ for collections manipulation when appropriate.
- Avoid empty catch blocks; at least log the exception.