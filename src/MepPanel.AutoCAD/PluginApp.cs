using Autodesk.AutoCAD.Runtime;

// Không dùng IExtensionApplication để tránh lỗi khởi động chặn đăng ký lệnh.
[assembly: CommandClass(typeof(MepPanel.AutoCAD.Commands.LicenseCommands))]
