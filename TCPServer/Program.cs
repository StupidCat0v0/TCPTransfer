using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

class Server
{
    static int Locality;
    static int size = 8192;
    static public List<string> FileListName = new List<string>();
    static public List<string> FileListAddress = new List<string>();
    private static void ListAllFilesAndDirectories(string path)
    {
        // 列出所有文件
        foreach (var file in Directory.EnumerateFiles(path))
        {
            FileListName.Add(file.Substring(Locality));
            FileListAddress.Add(file);
        }
        // 列出所有子文件夹
        foreach (var directory in Directory.EnumerateDirectories(path))
        {
            FileListName.Add(directory.Substring(Locality));
            FileListAddress.Add(directory);
            // 递归地列出子文件夹中的所有文件和文件夹
            ListAllFilesAndDirectories(directory);
        }
    }
    public static void Main()
    {
        // IP地址和端口号
        int port = 14514;

        IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (IPAddress ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                Console.WriteLine("本地IPv4地址: {0}", ip);
        }

        // 创建一个新的TcpListener实例
        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();

        Console.WriteLine("服务端启动，等待连接...");

        // 等待客户端连接
        TcpClient client = server.AcceptTcpClient();
        Console.WriteLine("客户端已连接");

        Console.Write("连接client方成功\n输入文件地址(将文件拖至此处)\n<<");
        string FileAddress = "C:\\Users\\Stupid Cat\\Desktop\\TCPTransfer - 副本\\新建文件夹";// Console.ReadLine();改一下地址

        // 移除两端的引号
        FileAddress = FileAddress.Trim('"');
        Locality = Path.GetDirectoryName(FileAddress).Length + 1;


        try
        {
            if (Directory.Exists(FileAddress))
            {
                Console.WriteLine("这是一个存在的文件夹。");
                try
                {
                    FileListName.Add(Path.GetFileName(FileAddress));
                    FileListAddress.Add(FileAddress);
                    ListAllFilesAndDirectories(FileAddress);
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine("没有权限访问该文件夹或其部分内容。");
                    return;
                }
            }
            else if (File.Exists(FileAddress))
            {
                Console.WriteLine("这是一个文件。");
                FileListName.Add(Path.GetFileName(FileAddress));
                FileListAddress.Add(FileAddress);
            }
            else
            {
                Console.WriteLine("这不是一个有效的路径，或者路径不存在。");
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("发生错误：" + ex.Message);
            return;
        }




        NetworkStream stream = client.GetStream();
        foreach (string s in FileListName)
        {
            Console.WriteLine(s);
            stream.Write(Encoding.Default.GetBytes(s + "\n"));
        }////////////////////////////////////////////////////////////发送////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////



        client.Close();
        server.Stop();
        Console.ReadKey();
    }
}