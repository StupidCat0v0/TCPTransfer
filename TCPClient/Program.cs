using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

class Client
{
    public static void Main(string[] args)
    {
        Console.Write("输入server ip地址：");
        // IP地址和端口号
        string ip = "127.0.0.1";//Console.ReadLine();
        int port = 14514;
        Console.WriteLine("服务端已连接");
        // 创建一个新的TcpClient实例
        TcpClient client = new TcpClient(ip, port);
        NetworkStream stream = client.GetStream();
        // 获取网络流
        for (int i = 0; i < 2; i++)
        {
            byte[] bytes = new byte[32768];
            stream.Read(bytes);
            string c = Encoding.Default.GetString(bytes, 0, 32768);
            if (c == " ")
                break;
            Console.WriteLine(c);
        }////////////////////////////////////////////////////////////
        // 清理资源
        stream.Close();
        client.Close();
        Console.ReadKey();
    }
}