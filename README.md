# 🌸 Reo — Code Like You Speak

**Reo** is a natural language programming language, compiler, and IDE built with ❤️ in C#.
It allows you to write code that reads like English, then compiles it into fully functional `.exe` applications via the Roslyn compiler.

> ✨ "Speak. Compile. Run." — **ReoLang** makes programming as intuitive as language itself.

---

## 📦 Features

### 🧠 ReoLang (The Language)
- English-like syntax: `let x be 10.`, `say "Hello".`, `repeat 5 times: ...`
- Built-in types: numbers, text, booleans, lists
- Control flow: `if`, `while`, `repeat`, `for each`
- Functions: define reusable logic like `to add(a, b): return a plus b.`
- I/O, time, string and list helpers included by default

### ⚙️ Reo Compiler
- Transforms `.reo` source → C# code → `.exe` using Roslyn
- Emits human-readable intermediate `.cs` file (optional)
- Syntax errors and build diagnostics included
- CLI and WPF IDE support

### 🪟 Reo Studio (Windows IDE)
- WPF-based modern desktop interface
- Create, edit, run and manage `.reo` programs
- Build/run output shown in real-time
- SQLite-powered history of programs, builds and runs

---

## ✍️ ReoLang Syntax Overview

```reo
# Variables
let name be "Joy".
set name to "Alex".
increase count by 1.

# Lists
let nums be [1, 2, 3].
append 4 to nums.

# Loops
repeat 3 times:
    say "Tick".
end repeat.

# Conditionals
if count is greater than 10, then:
    say "Big count!".
otherwise:
    say "Small count.".
end if.

# Functions
to greet(name):
    say "Hello, " plus name plus "!".
end.
```

---

## 🧰 Built-In Functions

| Function           | Description                             |
|--------------------|-----------------------------------------|
| `say(x)`           | Print value                             |
| `ask(prompt)`      | Get input from user                     |
| `length(x)`        | Count length of list or text            |
| `range(a, b)`      | Inclusive number list from a to b       |
| `now()`            | Current timestamp (string)              |
| `format_now(fmt)`  | Format datetime with custom format      |
| `to_number(x)`     | Cast to number                          |
| `to_text(x)`       | Cast to text                            |
| `to_truth(x)`      | Cast to boolean                         |
| `read_text(path)`  | Read text from file                     |
| `write_text(path, content)` | Write text to file            |

---

## 🧑‍💻 Reo Studio (IDE)

- ✅ Edit and save `.reo` programs
- ⚙️ Compile to `.exe` in one click
- ▶️ Run programs inside the app
- 🧾 Automatically stores program history, build logs, and run outputs

**Data is saved to:**
- Build folder: `%LOCALAPPDATA%\ReoStudio\Builds\`
- SQLite DB: `%LOCALAPPDATA%\ReoStudio\reo.db`

---

## 🧪 Reo CLI Usage

```bash
dotnet run --project Reo.CLI -- build demo.reo -o demo.exe
dotnet run --project Reo.CLI -- run demo.reo
```

### CLI Flags

| Flag              | Description                          |
|-------------------|--------------------------------------|
| `--emit-cs`       | Emit intermediate `.cs` file         |
| `--no-run`        | Skip running the compiled program    |
| `--no-db`         | Skip saving to database              |

---

## 🛠️ Build Instructions

### Prerequisites
- .NET SDK 8.0+
- Windows 10/11
- Visual Studio 2022+ (optional for IDE)

### Build All Projects

```bash
dotnet build ReoSuite.sln -c Release
```

### Run IDE

```bash
dotnet run --project Reo.Studio
```

### Run CLI

```bash
dotnet run --project Reo.CLI -- build myprog.reo -o out.exe
```

---

## 🧱 Project Structure

```
ReoSuite/
├── Reo.Core/        # Compiler engine (lexer, parser, codegen)
├── Reo.CLI/         # CLI interface
├── Reo.Studio/      # WPF IDE for Reo
├── Shared/          # Shared models and helpers
└── demo.reo         # Sample Reo program
```

---

## 🗄️ Database (SQLite via EF Core)

```plaintext
Programs
- ProgramId, Name, Source, CreatedAt

Builds
- BuildId, ProgramId, OutputPath, Diagnostics, Succeeded, BuiltAt

Runs
- RunId, ProgramId, Output, ExitCode, RanAt
```

---

## 📌 Roadmap

- [ ] Syntax highlighting via AvalonEdit
- [ ] REPL (interactive mode)
- [ ] Modules and imports (`use "utils.reo"`)
- [ ] Visual drag-to-code teaching mode
- [ ] Auto-updater and plugin system

---

## 📜 License

MIT License — Joy Sha  
Free for personal and educational use. Commercial licensing optional. Attribution appreciated.

---

## 🧾 Credits

- Language, Compiler, IDE — Joy Sha  
- Compiler engine: C#, Roslyn, .NET 8  
- IDE stack: WPF, MVVM, SQLite, EF Core  
- Inspired by natural programming, GPT-based language design, and the joy of clarity in code.

---

## ❤️ Made with love

> Reo was built to make programming feel like writing a story.  
> Simpler, safer, and more human.