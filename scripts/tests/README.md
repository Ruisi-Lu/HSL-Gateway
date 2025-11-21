# Test Scripts

This directory collects every interactive test script for HSL Gateway. All of them are written in portable `bash`/`sh`, so they run the same on Git Bash, WSL, Linux, or macOS.

## Available Scripts

| Script | Description |
| --- | --- |
| `multi-device.sh` | Launches the Modbus multi-device simulator set, the gateway (MultiDevice profile), and the multi-device test client (runs in the current terminal for interactive input). |
| `subscription.sh` | Launches a single simulator, the gateway, and the subscription test client with a guided workflow. |

## Usage

```bash
# Run from the repository root
scripts/tests/multi-device.sh
scripts/tests/subscription.sh
```

Each script will:

1. Run `dotnet build` for the required projects.
2. Ask whether to auto-start everything or just print manual steps.
3. When auto-starting, spawn every service in the same terminal session and shut them down together when you press `Ctrl+C`.


### Automation

Set `HSL_TEST_AUTO=1` **or** pass `--auto`/`-a` to `multi-device.sh` if you need a fully scripted run (e.g., CI). The simulator and gateway will still run in the background, but the multi-device test client switches to its built-in `--auto-demo` flow so it can execute a deterministic scenario and exit without manual input.

> On Windows/Git Bash ensure `dotnet` is on `PATH`. On WSL, Linux, or macOS you can use the system-installed .NET SDK.
