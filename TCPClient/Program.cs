using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

class Client
{
    const int BufferSize = 81920; // 增大缓冲区提升传输效率

    static async Task Main(string[] args)
    {
        Console.Write("服务器IP地址：");
        string ip = Console.ReadLine();
        Console.Write("服务器端口号：");
        int port = int.Parse(Console.ReadLine());

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ip, port);// 连接服务器
            Console.WriteLine("已连接服务器");

            using var stream = client.GetStream();
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

            var mode = reader.ReadInt32();//模式 1: 文件夹接收, 2: 单文件接收
            var downloadsPath = GetDownloadsPath();// 获取下载目录

            switch (mode)
            {
                case 1:
                    await ReceiveDirectoryAsync(reader, downloadsPath);// 文件夹接收
                    break;
                case 2:
                    await ReceiveSingleFileAsync(reader, downloadsPath);// 单文件接收
                    break;
                default:
                    Console.WriteLine("未知传输模式");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"发生错误: {ex.Message}");
        }
        Console.ReadKey();
    }

    static async Task ReceiveDirectoryAsync(BinaryReader reader, string basePath)
    {
        try
        {
            // 读取文件夹结构
            var dirCount = reader.ReadInt32();// 文件夹数量
            var directories = new List<string>(dirCount);// 文件夹列表

            for (int i = 0; i < dirCount; i++)
            {
                directories.Add(reader.ReadString());// 读取文件夹名称
            }

            // 创建文件夹结构
            var rootDir = GetUniquePath(basePath, directories[0]);// 获取唯一文件夹路径
            foreach (var dir in directories)
            {
                var fullPath = Path.Combine(rootDir, dir);// 获取完整路径
                Directory.CreateDirectory(fullPath);// 创建文件夹
                Console.WriteLine($"创建文件夹: {fullPath}");// 创建文件夹
            }

            // 发送确认
            await SendAck(reader.BaseStream);

            // 接收文件
            var fileCount = reader.ReadInt32();
            for (int i = 0; i < fileCount; i++)
            {
                var relativePath = reader.ReadString();// 读取相对路径
                var fileSize = reader.ReadInt64();// 读取文件大小
                var savePath = Path.Combine(rootDir, relativePath);// 获取保存路径

                Console.WriteLine($"接收文件: {relativePath} ({FormatFileSize(fileSize)})");// 显示接收文件信息
                await ReceiveFileAsync(reader.BaseStream, savePath, fileSize);// 接收文件
                await SendAck(reader.BaseStream);// 发送确认
            }
            Console.WriteLine("文件夹接收完成");
            Process.Start("Explorer.exe", "/select," + rootDir);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"接收文件夹错误: {ex.Message}");
        }
    }

    static async Task ReceiveSingleFileAsync(BinaryReader reader, string basePath)
    {
        try
        {
            var fileName = reader.ReadString();// 读取文件名称
            var fileSize = reader.ReadInt64();// 读取文件大小
            var savePath = GetUniqueFilePath(basePath, fileName);// 获取唯一文件路径

            Console.WriteLine($"接收文件: {fileName} ({FormatFileSize(fileSize)})");// 显示接收文件信息
            await ReceiveFileAsync(reader.BaseStream, savePath, fileSize);// 接收文件
            await SendAck(reader.BaseStream);// 发送确认
            Console.WriteLine("文件接收完成");
            Process.Start("Explorer.exe", "/select," + savePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"接收文件错误: {ex.Message}");
        }
    }

    static async Task ReceiveFileAsync(Stream stream, string savePath, long fileSize)
    {
        using var fs = new FileStream(savePath, FileMode.Create, FileAccess.Write);// 创建文件流
        var buffer = new byte[BufferSize];// 缓冲区
        long bytesReceived = 0;

        while (bytesReceived < fileSize)
        {
            var bytesToRead = (int)Math.Min(buffer.Length, fileSize - bytesReceived);// 计算要读取的字节数
            var bytesRead = await stream.ReadAsync(buffer, 0, bytesToRead); // 读取数据
            if (bytesRead == 0) throw new EndOfStreamException();// 读取结束异常

            await fs.WriteAsync(buffer, 0, bytesRead);// 写入文件
            bytesReceived += bytesRead;// 更新已接收字节数

            // 显示进度
            Console.Write($"\r进度: {bytesReceived * 100 / fileSize}% ({bytesReceived}/{fileSize})");
        }
        Console.WriteLine();
    }

    static string GetDownloadsPath()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");// 获取下载目录
        return Directory.Exists(path) ? path : Directory.CreateDirectory(path).FullName;
    }

    static string GetUniquePath(string basePath, string name)
    {
        var path = Path.Combine(basePath, name);// 获取完整路径
        for (int i = 1; Directory.Exists(path); i++)
        {
            path = Path.Combine(basePath, $"{name} ({i})");// 获取唯一路径
        }
        return path;
    }

    static string GetUniqueFilePath(string basePath, string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);// 获取文件名
        var ext = Path.GetExtension(fileName);// 获取扩展名
        var path = Path.Combine(basePath, fileName);// 获取完整路径

        for (int i = 1; File.Exists(path); i++)
        {
            path = Path.Combine(basePath, $"{name} ({i}){ext}");// 获取唯一路径
        }
        return path;
    }

    static async Task SendAck(Stream stream)
    {
        var ack = BitConverter.GetBytes(1);
        await stream.WriteAsync(ack, 0, ack.Length);// 发送确认
    }

    static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };// 文件大小单位
        int order = 0;
        double len = bytes;
        while (len >= 1024 && order < sizes.Length - 1)// 计算单位
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";// 格式化文件大小
    }
}