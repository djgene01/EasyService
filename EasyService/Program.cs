using Terminal.Gui;
using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using SQLitePCL;
using System.Collections.Generic;

namespace EasyService
{
    class Program
    {
        static SqliteConnection dbConnection;
        static string dbPath = "services.db";
        static string selectedExecPath = "";
        static bool useDotnetDll = false;

        static void Main(string[] args)
        {
            Batteries.Init();
            InitDatabase();
            Application.Init();
            var top = Application.Top;

            var win = new Window("EasyService - Debian Service Manager")
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            var menu = new MenuBar(new MenuBarItem[] {
                new MenuBarItem("_File", new MenuItem[] {
                    new MenuItem("_Quit", "", () => Application.RequestStop())
                }),
                new MenuBarItem("_Service", new MenuItem[] {
                    new MenuItem("_Create", "", ShowCreateServiceDialog),
                    new MenuItem("_Manage", "", ShowManageServicesDialog)
                })
            });

            top.Add(menu, win);
            Application.Run();
        }

        static void InitDatabase()
        {
            dbConnection = new SqliteConnection($"Data Source={dbPath}");
            dbConnection.Open();

            var command = dbConnection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Services (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Description TEXT,
                    ExecPath TEXT NOT NULL,
                    IsDotnetDll INTEGER NOT NULL DEFAULT 0
                )";
            command.ExecuteNonQuery();
        }

        static void ShowCreateServiceDialog()
        {
            var dialog = new Dialog("Create Service", 60, 22);

            var nameLabel = new Label("Service Name:") { X = 1, Y = 2 };
            var nameField = new TextField("") { X = 15, Y = 2, Width = 40 };

            var descLabel = new Label("Description:") { X = 1, Y = 4 };
            var descField = new TextField("") { X = 15, Y = 4, Width = 40 };

            var dotnetCheck = new CheckBox("Use dotnet DLL") { X = 1, Y = 6 };
            dotnetCheck.Toggled += (e) => useDotnetDll = dotnetCheck.Checked;

            var execLabel = new Label("File Path:") { X = 1, Y = 8 };
            var execPathDisplay = new Label("") { X = 15, Y = 8, Width = 40 };
            var selectExecButton = new Button("Select") { X = 15, Y = 10 };

            selectExecButton.Clicked += () =>
            {
                var fileDialog = new OpenDialog("Select File", useDotnetDll ? "Select the .NET DLL for the service" : "Select the executable file for the service");
                Application.Run(fileDialog);
                if (!fileDialog.Canceled && fileDialog.FilePath != null)
                {
                    selectedExecPath = fileDialog.FilePath.ToString();
                    execPathDisplay.Text = selectedExecPath;
                }
            };

            var createButton = new Button("Create") { X = 15, Y = 12 };
            var cancelButton = new Button("Cancel") { X = 25, Y = 12 };

            createButton.Clicked += () =>
            {
                if (string.IsNullOrEmpty(nameField.Text.ToString()) || string.IsNullOrEmpty(selectedExecPath))
                {
                    MessageBox.ErrorQuery("Error", "Name and File Path are required", "OK");
                    return;
                }

                CreateService(
                    nameField.Text.ToString(),
                    descField.Text.ToString(),
                    selectedExecPath,
                    useDotnetDll
                );
                Application.RequestStop(dialog);
            };

            cancelButton.Clicked += () => Application.RequestStop(dialog);

            dialog.Add(nameLabel, nameField, descLabel, descField, dotnetCheck, execLabel, execPathDisplay, selectExecButton, createButton, cancelButton);
            Application.Run(dialog);
        }

        static void CreateService(string name, string description, string execPath, bool isDotnetDll)
        {
            var fullName = name + ".service";
            var serviceFile = $"/etc/systemd/system/{fullName}";
            var execStart = isDotnetDll ? $"/usr/bin/dotnet {execPath}" : execPath;
            var serviceContent = $@"[Unit]
Description={description}

[Service]
ExecStart={execStart}
Restart=always

[Install]
WantedBy=multi-user.target";

            File.WriteAllText(serviceFile, serviceContent);
            ExecuteCommand($"systemctl enable {fullName}");
            ExecuteCommand($"systemctl start {fullName}");

            var command = dbConnection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Services (Name, Description, ExecPath, IsDotnetDll)
                VALUES ($name, $desc, $exec, $isDotnet)";
            command.Parameters.AddWithValue("$name", fullName);
            command.Parameters.AddWithValue("$desc", description ?? "");
            command.Parameters.AddWithValue("$exec", execPath);
            command.Parameters.AddWithValue("$isDotnet", isDotnetDll ? 1 : 0);
            command.ExecuteNonQuery();
        }

        static void ShowManageServicesDialog()
        {
            var dialog = new Dialog("Manage Services", 80, 25);
            var listView = new ListView
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill() - 2,
                Height = Dim.Fill() - 4,
                AllowsMarking = false
            };

            var services = GetServices();
            listView.SetSource(services.Select(s => s.ToString()).ToList());

            var startButton = new Button("Start") { X = 1, Y = Pos.Bottom(listView) + 1 };
            var stopButton = new Button("Stop") { X = 10, Y = Pos.Bottom(listView) + 1 };
            var deleteButton = new Button("Delete") { X = 19, Y = Pos.Bottom(listView) + 1 };
            var closeButton = new Button("Close") { X = 30, Y = Pos.Bottom(listView) + 1 };

            startButton.Clicked += () =>
            {
                if (listView.SelectedItem >= 0 && listView.SelectedItem < services.Count)
                {
                    var service = services[listView.SelectedItem];
                    ExecuteCommand($"systemctl start {service.Name}");
                    services = GetServices();
                    listView.SetSource(services.Select(s => s.ToString()).ToList());
                }
            };

            stopButton.Clicked += () =>
            {
                if (listView.SelectedItem >= 0 && listView.SelectedItem < services.Count)
                {
                    var service = services[listView.SelectedItem];
                    ExecuteCommand($"systemctl stop {service.Name}");
                    services = GetServices();
                    listView.SetSource(services.Select(s => s.ToString()).ToList());
                }
            };

            deleteButton.Clicked += () =>
            {
                if (listView.SelectedItem >= 0 && listView.SelectedItem < services.Count)
                {
                    var service = services[listView.SelectedItem];
                    if (service.IsManaged)
                    {
                        ExecuteCommand($"systemctl disable {service.Name}");
                        ExecuteCommand($"systemctl stop {service.Name}");
                        File.Delete($"/etc/systemd/system/{service.Name}");
                        ExecuteCommand("systemctl daemon-reload");
                        DeleteService(service.Name);
                        services = GetServices();
                        listView.SetSource(services.Select(s => s.ToString()).ToList());
                    }
                    else
                    {
                        MessageBox.ErrorQuery("Error", "Cannot delete services not created through this app", "OK");
                    }
                }
            };

            closeButton.Clicked += () => Application.RequestStop(dialog);

            dialog.Add(listView, startButton, stopButton, deleteButton, closeButton);
            Application.Run(dialog);
        }

        static List<Service> GetServices()
        {
            var managedServices = GetManagedServices();
            var systemServices = GetSystemServices();
            var services = new List<Service>();
            foreach (var sys in systemServices)
            {
                if (managedServices.TryGetValue(sys.Name, out var managed))
                {
                    services.Add(new Service
                    {
                        Name = sys.Name,
                        Status = sys.Status,
                        Description = managed.Description,
                        ExecPath = managed.ExecPath,
                        IsManaged = true,
                        IsDotnetDll = managed.IsDotnetDll
                    });
                }
                else
                {
                    services.Add(new Service
                    {
                        Name = sys.Name,
                        Status = sys.Status,
                        IsManaged = false
                    });
                }
            }
            return services;
        }

        static Dictionary<string, Service> GetManagedServices()
        {
            var command = dbConnection.CreateCommand();
            command.CommandText = "SELECT Name, Description, ExecPath, IsDotnetDll FROM Services";
            using var reader = command.ExecuteReader();
            var managed = new Dictionary<string, Service>();
            while (reader.Read())
            {
                var name = reader.GetString(0);
                var desc = reader.IsDBNull(1) ? null : reader.GetString(1);
                var exec = reader.GetString(2);
                var isDotnet = reader.GetInt32(3) == 1;
                managed[name] = new Service { Name = name, Description = desc, ExecPath = exec, IsDotnetDll = isDotnet };
            }
            return managed;
        }

        static List<(string Name, string Status)> GetSystemServices()
        {
            var output = ExecuteCommandWithOutput("systemctl list-units --type=service --all --plain --no-legend");
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var systemServices = new List<(string Name, string Status)>();
            foreach (var line in lines)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    var name = parts[0];
                    var subState = parts[3]; // sub state
                    systemServices.Add((name, subState));
                }
            }
            return systemServices;
        }

        static string ExecuteCommandWithOutput(string command)
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{command}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output;
        }

        static void DeleteService(string name)
        {
            var command = dbConnection.CreateCommand();
            command.CommandText = "DELETE FROM Services WHERE Name = $name";
            command.Parameters.AddWithValue("$name", name);
            command.ExecuteNonQuery();
        }

        static void ExecuteCommand(string command)
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{command}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit();
        }
    }

    class Service
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string ExecPath { get; set; }
        public string Status { get; set; }
        public bool IsManaged { get; set; }
        public bool IsDotnetDll { get; set; }

        public override string ToString()
        {
            return IsManaged ? $"{Name} ({Status}) [managed{(IsDotnetDll ? ", dotnet]" : "]")}" : $"{Name} ({Status})";
        }
    }
}