Build / Rebuild

Open a terminal or command prompt.

Navigate to your project directory using cd path/to/your/project (C:\Users\psyjo\source\repos\psyBrowser\).

Run the following command:
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true

This will create a self-contained executable in the bin\Release\{cur-ver}\win-x64\publish directory.
C:\Users\psyjo\source\repos\psyBrowser\bin\Release\net9.0-windows\win-x64\publish

-----------------------------
Upgrade .NET Core

Open psyBrowser.csproj

Update the version in <TargetFramework> tag

Rebuild

-----------------------------
Upgrade browsing engine (Chromium via CEF)
This should be done just about every day if we were keeping on top of it but at LEAST every time we go to look at the code it should be done.

Open Visual Studio and the project

Right-click the project name in the Solution Explorer and select "Manage NuGet Packages"

If there is NOT an update for chromium it would be surprising and it will usually be the only thing (because its the only package we use)

Check the box and hit the 'Update' button
(It will take a bit to do its thing eventually providing a confirmation of what will be adjusted for you to accept)

Once you've accepted and applied the changes, rebuild the application for release