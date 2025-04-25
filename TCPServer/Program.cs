using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

class Server
{
    const int BufferSize = 81920; // 增大缓冲区提升传输效率
    const int Port = 14514;

    class FileItem
    {
        public string RelativePath { get; set; }
        public string FullPath { get; set; }
        public long FileSize { get; set; }
    }

    static async Task Main(string[] args)
    {
        var listener = new TcpListener(IPAddress.Any, Port);// 监听所有IP地址
        listener.Start();
        Console.WriteLine($"服务端启动，监听端口 {Port}...");

        try
        {
            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();// 异步接受客户端连接
                Console.WriteLine("客户端已连接");
                DisplayLocalIPAddresses();
                _ = HandleClientAsync(client); // 异步处理客户端连接
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    static async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                Console.Write("输入文件/文件夹路径：");
                var path = Console.ReadLine().Trim('"');

                if (Directory.Exists(path))
                {
                    await SendDirectoryAsync(path, writer, reader);
                }
                else if (File.Exists(path))
                {
                    await SendSingleFileAsync(path, writer, reader);
                }
                else
                {
                    writer.Write(0); // 无效模式
                    Console.WriteLine("路径不存在");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"处理客户端错误: {ex.Message}");
        }
    }
    /// <summary>
    /// 将指定目录的内容（包括子目录和文件）发送到远程客户端。
    /// </summary>
    /// <param name="dirPath">指定要发送的目录的路径</param>
    /// <param name="writer">用于将目录和文件信息写入远程客户端</param>
    /// <param name="reader">便于在传输过程中从远程客户端读取确认</param>
    /// <returns>完成异步操作而不返回值</returns>
    static async Task SendDirectoryAsync(string dirPath, BinaryWriter writer, BinaryReader reader)
    {
        try
        {
            var baseDir = new DirectoryInfo(dirPath);
            var fileItems = new List<FileItem>();
            var dirItems = new List<string>();


            ScanDirectory(baseDir, baseDir.Parent.FullName, fileItems, dirItems);

            writer.Write(1); // 文件夹模式
            writer.Write(dirItems.Count);// 发送文件夹数量

            foreach (var dir in dirItems)
            {
                Console.WriteLine(dir);
                writer.Write(dir);// 发送文件夹信息
            }
            await WaitForAck(reader);// 等待客户端确认

            writer.Write(fileItems.Count);// 发送文件数量
            foreach (var file in fileItems)
            {
                Console.WriteLine(file.RelativePath);// 相对路径
                Console.WriteLine(file.FullPath);// 完整路径
                Console.WriteLine(file.FileSize);// 文件大小
                writer.Write(file.RelativePath);// 发送相对路径名
                writer.Write(file.FileSize);// 发送文件大小

                using (var fs = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read))
                {
                    await fs.CopyToAsync(writer.BaseStream).ConfigureAwait(false);// 发送文件内容
                }
                await WaitForAck(reader);// 等待客户端确认
            }// 发送文件信息
            Console.WriteLine("文件夹发送完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"发送文件夹错误: {ex.Message}");
            writer.Write(-1); // 错误标识
        }
    }
    /// <summary>
    /// 异步向客户端发送单个文件，包括其名称和大小，并等待确认。
    /// </summary>
    ///<param name=“filePath”>指定要发送的文件的位置</param>
    ///<param name=“writer”>用于将文件信息和内容写入目标流</param>
    ///<param name=“reader”>便于在文件传输后读取客户端的确认</param>
    ///<returns>此方法不返回值</returns>
    static async Task SendSingleFileAsync(string filePath, BinaryWriter writer, BinaryReader reader)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            writer.Write(2); // 文件模式
            writer.Write(fileInfo.Name);// 发送文件名
            writer.Write(fileInfo.Length);// 发送文件大小

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                await fs.CopyToAsync(writer.BaseStream).ConfigureAwait(false);// 发送文件内容
            }
            await WaitForAck(reader);// 等待客户端确认
            Console.WriteLine("文件发送完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"发送文件错误: {ex.Message}");
            writer.Write(-1);
        }
    }
    /// <summary>
    /// 扫描目录及其子目录，收集有关文件和目录的信息。
    /// </summary>
    /// <param name="dir">指定要扫描文件和子目录的目录</param>
    /// <param name="rootPath">定义用于计算文件和目录相对路径的基本路径</param>
    /// <param name="fileItems">存储扫描期间发现的文件的信息</param>
    /// <param name="dirItems">保存扫描中遇到的目录的相对路径</param>
    static void ScanDirectory(DirectoryInfo dir, string rootPath, List<FileItem> fileItems, List<string> dirItems)
    {
        var relativePath = dir.FullName.Substring(rootPath.Length + 1);// 获取相对路径
        dirItems.Add(relativePath);// 添加文件夹信息

        foreach (var file in dir.GetFiles())
        {
            fileItems.Add(new FileItem
            {
                RelativePath = file.FullName.Substring(rootPath.Length + 1),
                FullPath = file.FullName,
                FileSize = file.Length
            });// 添加文件信息
        }

        foreach (var subDir in dir.GetDirectories())
        {
            ScanDirectory(subDir, rootPath, fileItems, dirItems);// 递归扫描子文件夹
        }
    }


    static async Task WaitForAck(BinaryReader reader)
    {
        var ackBuffer = new byte[4];
        int bytesRead = await reader.BaseStream.ReadAsync(ackBuffer, 0, 4);
        if (bytesRead != 4 || BitConverter.ToInt32(ackBuffer, 0) != 1)
            throw new InvalidOperationException("客户端确认失败");
    }

    static void DisplayLocalIPAddresses()
    {
        foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                Console.WriteLine($"本地IPv4地址: {ip}");
        }
    }
}