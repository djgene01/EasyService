EasyService
EasyService is a console-based GUI application for managing Debian system services, built using .NET 9 and Terminal.Gui. It simplifies the creation and management of systemd services, allowing users to create new services, start/stop existing services, and delete services created through the app. The application supports running services directly via executables or using dotnet with DLLs, and it lists all system services for comprehensive management.
Features

Create Services: Easily create new systemd services with a user-friendly interface, including a file browser for selecting executables or .NET DLLs.
Run via Dotnet: Option to start services using dotnet for .NET DLLs.
Manage Services: View, start, and stop all system services; delete services created through the app.
SQLite Database: Stores metadata for services created by the app, ensuring persistence and easy management.
Terminal-Based GUI: Intuitive interface built with Terminal.Gui, suitable for console environments.
Safety: Restricts deletion to app-created services to prevent accidental removal of critical system services.

Prerequisites

Operating System: Debian-based Linux distribution (e.g., Ubuntu, Debian).
.NET SDK: Version 9.0 or later.
System Permissions: Root privileges (e.g., sudo) to create and manage systemd service files.
Dependencies:
Terminal.Gui (v2.0.0)
Microsoft.Data.Sqlite (v9.0.0)
SQLitePCLRaw.bundle_e_sqlite3 (v2.1.8)



Installation

Clone the Repository:
git clone https://github.com/your-username/EasyService.git
cd EasyService


Restore Dependencies:
dotnet restore


Build the Project:
dotnet build


Run the Application:
sudo dotnet run

Note: sudo is required to manage systemd services.


Usage

LaunchSources: Launch the application with sudo dotnet run.

Create a Service:

Navigate to Service > Create.
Enter a service name and optional description.
Check "Use dotnet DLL" if the service should run a .NET DLL.
Click "Select" to browse and choose an executable or DLL.
Click "Create" to generate and start the service.


Manage Services:

Navigate to Service > Manage.
View all system services, with app-created services marked as [managed] or [managed, dotnet].
Select a service to start, stop, or delete (only for managed services).



Project Structure

Program.cs: Main application logic, including GUI, service management, and database operations.
EasyService.csproj: Project file specifying dependencies and .NET 9 target framework.
services.db: SQLite database storing metadata for managed services.

Contributing
Contributions are welcome! Please follow these steps:

Fork the repository.
Create a feature branch (git checkout -b feature/YourFeature).
Commit your changes (git commit -m "Add YourFeature").
Push to the branch (git push origin feature/YourFeature).
Open a Pull Request.

License
This project is licensed under the MIT License. See the LICENSE file for details.
Acknowledgments

Built with Terminal.Gui for the console-based UI.
Uses Microsoft.Data.Sqlite for database management.
Inspired by the need for a simple, GUI-based service manager for Debian systems.

