
# MicroRun

**MicroRun** is a WPF application built with .NET 9 that simplifies the process of running specific .NET Core microservices during testing and development. It allows you to select and run individual microservices with ease, using existing Visual Studio launch configurations, thus saving time and improving productivity.

## Features

- **Simple Service Selection**: Choose specific microservices to run instead of launching all services manually.
- **Persistent Settings**: Remembers your selected services and configurations for future use.
- **Visual Studio Integration**: Works seamlessly with existing Visual Studio launch settings (e.g., launch profiles).
- **Built for .NET 9**: Developed using WPF and .NET 9, ensuring modern and efficient performance.
- **Streamlined Testing**: Simplifies the testing of microservices by focusing on the ones you need, without running unnecessary services.

## Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/MicroRun.git
   ```

2. Open the solution in Visual Studio.

3. Build and run the project.

## Usage

1. Open **MicroRun**.
2. Select the .NET Core services you want to run. The utility will display available projects and let you choose which ones to start.
3. Click **Start** to launch the selected services. You can start all services or just the ones you need.
4. The utility remembers your selections, so you don’t need to reconfigure it each time.

**Note**: MicroRun uses your existing Visual Studio project configurations, so no additional setup is required for the services you're running.

## Contributing

Contributions are welcome! To contribute to the project, follow these steps:

1. Fork the repository.
2. Create a new branch (`git checkout -b feature-name`).
3. Commit your changes (`git commit -am 'Add feature'`).
4. Push to the branch (`git push origin feature-name`).
5. Open a pull request.

## License

This project is licensed under the Apache 2.0 License
