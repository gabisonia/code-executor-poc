using CodeExecutor;
using Spectre.Console;

AnsiConsole.MarkupLine("[bold yellow]Python Code Executor[/]");

AnsiConsole.MarkupLine("Enter your Python code (type '[green]END[/]' on a new line to finish):");

var executor = new PythonExecutor();

var code = "print(\"Hello this is test run\")";

var result = await executor.ExecuteCodeInDocker(code);

AnsiConsole.Write(new Panel(result)
    .Header("[bold green]Execution Result[/]")
    .BorderColor(Color.Green)
    .RoundedBorder());
    