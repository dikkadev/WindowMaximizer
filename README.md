# WindowMaximizer

## Functionality

WindowMaximizer is a utility that allows you to maximize the currently active window to a specified monitor. If the window is already maximized, it will restore the window to its previous size and position. This is useful for users who work with multiple monitors and want to quickly move and maximize windows across different screens.

## How to Build Locally

To build the project locally, you can use the following command:

```sh
dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true --self-contained true
```

This command will create a self-contained executable file in the `.\bin\Release\net8.0-windows\win-x64\` directory.

## GitHub Actions Workflow

This repository includes a GitHub Actions workflow that automatically builds and releases the executable file on each push to the main branch. The workflow is defined in the `.github/workflows/build-and-release.yml` file.

## Shortcut Hotkeys

To handle the shortcut hotkeys, it is recommended to use a tool like Clavier+. You can download Clavier+ from the following link: [Clavier+](https://gryder.org/software/clavier-plus/)
