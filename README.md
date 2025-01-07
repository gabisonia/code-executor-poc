# Code Executor POC

## Overview
Code Executor is a C# console application that allows users to write and execute scripts within isolated Docker containers. 
This proof of concept (POC) currently supports Python execution, with plans to extend support to additional programming languages in the future.
Executing code inside Docker containers ensures that scripts run in a controlled environment, enhancing security and consistency across different systems.

## Prerequisites
Before you begin, ensure you have the following installed and running on your computer:

**Docker**: Docker must be installed and the Docker daemon should be running.

**.NET 9.0**: Ensure that .NET 9.0 is installed.

## Usage

Replace code
```
var code = "print(\"Hello this is test run\")";
```

Navigate to the project directory and execute the application.

```bash
dotnet run
```

## Dependencies

- **Docker.DotNet:** A .NET client library for interacting with Docker APIs.
- **Spectre.Console:** A library for creating beautiful console applications.

These dependencies are managed via NuGet and are specified in the `.csproj` file.

## License

No Licence, project is fully free to use, modify, and distribute for any purpose, personal or commercial. 
