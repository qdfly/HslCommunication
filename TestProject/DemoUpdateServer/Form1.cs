﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using HslCommunication;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;

namespace DemoUpdateServer
{
    public partial class Form1 : Form
    {
        public Form1( )
        {
            InitializeComponent( );
        }

        private void Form1_Load( object sender, EventArgs e )
        {
            // 加载版本号
            if (File.Exists( "version.txt" ))
            {
                textBox1.Text = Encoding.Default.GetString( File.ReadAllBytes( "version.txt" ) );
                version = new HslCommunication.BasicFramework.SystemVersion( textBox1.Text );
            }

            if(!Directory.Exists( Application.StartupPath + @"\Demo" ))
            {
                Directory.CreateDirectory( Application.StartupPath + @"\Demo" );
            }

            lognet = new HslCommunication.LogNet.LogNetSingle( "logs.txt" );
            lognet.BeforeSaveToFile += LogNet_BeforeSaveToFile;

            LoadData( );
            timer.Interval = 3000;
            timer.Tick += Timer_Tick;
            timer.Start( );
        }


        private void button1_Click( object sender, EventArgs e )
        {
            version = new HslCommunication.BasicFramework.SystemVersion( textBox1.Text );
            File.WriteAllBytes( "version.txt",Encoding.Default.GetBytes( textBox1.Text ) );

            MessageBox.Show( "更新成功" );
        }

        private HslCommunication.BasicFramework.SystemVersion version = new HslCommunication.BasicFramework.SystemVersion( "1.0.1" );
        private HslCommunication.LogNet.ILogNet lognet;
        private Random random = new Random( );
        private Timer timer = new Timer( );

        #region 同步网络中心，用来请求版本号信息

        private HslCommunication.Enthernet.NetSimplifyServer simplifyServer;

        private void NetStart( )
        {
            simplifyServer = new HslCommunication.Enthernet.NetSimplifyServer( );
            simplifyServer.ReceiveStringEvent += SimplifyServer_ReceiveStringEvent;
            simplifyServer.ServerStart( 18467 );
        }

        private void SimplifyServer_ReceiveStringEvent( HslCommunication.Core.Net.AppSession arg1, NetHandle handle, string msg )
        {
            if(handle == 1)
            {
                simplifyServer.SendMessage( arg1, handle, version.ToString( ) );
                string address = GetAddressByIp( arg1.IpAddress );
                lognet.WriteInfo( $"{arg1.IpAddress.PadRight( 15 )} [{msg.PadRight( 8 )}] [{address}]" );
                AddDict( address );
            }
            else if(handle == 2)
            {
                simplifyServer.SendMessage( arg1, random.Next( 10000 ), "这是一条测试的数据：" + random.Next( 10000 ).ToString( ) );
            }
            else
            {
                simplifyServer.SendMessage( arg1, handle, msg );
            }
        }


        #endregion

        #region UpdateServer

        private HslCommunication.Enthernet.NetSoftUpdateServer softUpdateServer;

        private void NetStart2( )
        {
            softUpdateServer = new HslCommunication.Enthernet.NetSoftUpdateServer( );
            softUpdateServer.FileUpdatePath = Application.StartupPath + @"\Demo";
            //softUpdateServer.LogNet = lognet;
            softUpdateServer.ServerStart( 18468 );
        }

        private void LogNet_BeforeSaveToFile( object sender, HslCommunication.LogNet.HslEventArgs e )
        {
            Invoke( new Action( ( ) =>
             {
                 if (e.HslMessage.Degree != HslCommunication.LogNet.HslMessageDegree.FATAL)
                 {
                     textBox2.AppendText( e.HslMessage.ToString( ) + Environment.NewLine );
                 }
             } ) );
        }

        #endregion

        private void Form1_Shown( object sender, EventArgs e )
        {
            NetStart( );
            NetStart2( );
            Timer_Tick( null, new EventArgs( ) );
        }

        private void button2_Click( object sender, EventArgs e )
        {
            MessageBox.Show( GetAddressByIp( "47.92.5.140" ) );
        }

        private string GetAddressByIp(string ip )
        {
            try
            {
                WebClient webClient = new WebClient( );

                webClient.Encoding = Encoding.UTF8;
                webClient.Headers.Add( "Accept", "image/gif, image/x-xbitmap, image/jpeg, image/pjpeg, application/x-shockwave-flash, application/vnd.ms-excel, application/vnd.ms-powerpoint, application/msword, */*" );
                webClient.Headers.Add( "Accept-Language", "zh-cn" );//ja-jp
                webClient.Headers.Add( "User-Agent", "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/39.0.2171.71" );
                webClient.Headers.Add( "Content-Type", "text/html" );
                webClient.Headers.Add( "Content-Type", "image/jpeg" );
                //webClient.Headers.Add("Connection", "Keep-Alive");
                webClient.Headers.Add( "Accept-Encoding", "gzip,deflate" );

                byte[] data = webClient.DownloadData( "http://www.ip138.com/ips138.asp?ip=" + ip + "&action=2" );
                webClient.Dispose( );

                string result = Encoding.Default.GetString( data );

                Match match = Regex.Match( result, "<ul class=\"ul1\"><li>[^<]+" );
                if (match == null)
                {
                    return string.Empty;
                }

                return match.Value.Substring( 25 );
            }
            catch
            {
                return string.Empty;
            }
        }


        private Dictionary<string, long> loginData = new Dictionary<string, long>( );
        private HslCommunication.Core.SimpleHybirdLock hybirdLock = new HslCommunication.Core.SimpleHybirdLock( );

        private void AddDict(string address )
        {
            if (string.IsNullOrEmpty( address )) return;

            if(address.IndexOf(' ' ) > 0)
            {
                address = address.Substring( 0, address.IndexOf( ' ' ) );
            }

            hybirdLock.Enter( );
            if (loginData.ContainsKey( address ))
            {
                loginData[address]++;
            }
            else
            {
                loginData.Add( address, 1 );
            }
            hybirdLock.Leave( );

            countOld++;
        }

        private void LoadData( )
        {
            hybirdLock.Enter( );
            loginData.Clear( );
            if (File.Exists( "City.txt" ))
            {
                StreamReader sr = new StreamReader( "City.txt", Encoding.Default );
                while (true)
                {
                    string city = sr.ReadLine( );
                    if (city == null) break;

                    string count = sr.ReadLine( );
                    loginData.Add( city, long.Parse( count ) );
                }
                sr.Close( );
            }


            hybirdLock.Leave( );
        }

        private void SaveData( )
        {
            hybirdLock.Enter( );

            StreamWriter sw = new StreamWriter( "City.txt", false, Encoding.Default );
            foreach(var m in loginData)
            {
                sw.WriteLine( m.Key );
                sw.WriteLine( m.Value );
            }
            sw.Close( );
            hybirdLock.Leave( );
        }


        private long countOld = 1;
        private void Timer_Tick( object sender, EventArgs e )
        {
            if (countOld == 0) return;
            countOld = 0;
            long Count = 0;

            List<dataMy> list = new List<dataMy>( );   
            hybirdLock.Enter( );

            foreach (var m in loginData)
            {
                list.Add( new dataMy( m.Key, m.Value ) );
            }

            hybirdLock.Leave( );

            list.Sort( );
            list.Reverse( );

            while (dataGridView1.RowCount < list.Count)
            {
                dataGridView1.Rows.Add( );
            }

            while (dataGridView1.RowCount > list.Count)
            {
                dataGridView1.Rows.RemoveAt( 0 );
            }

            // 赋值
            for (int i = 0; i < list.Count; i++)
            {
                dataGridView1.Rows[i].Cells[0].Value = list[i].Key;
                dataGridView1.Rows[i].Cells[1].Value = list[i].Value.ToString( );
                Count += list[i].Value;
            }

            label2.Text = "总计：" + Count.ToString( );

            // 统计省份功能
            List<dataMy> shengfen = new List<dataMy>( );
            for (int i = 0; i < list.Count; i++)
            {
                string tmp = string.Empty;
                if (list[i].Key.IndexOf( '省' ) > 0)
                {
                    tmp = list[i].Key.Substring( 0, list[i].Key.IndexOf( '省' ) + 1 );
                }
                else if (list[i].Key.Contains( "北京市" ))
                {
                    tmp = "北京市";
                }
                else if (list[i].Key.Contains( "上海市" ))
                {
                    tmp = "上海市";
                }
                else if (list[i].Key.Contains( "天津市" ))
                {
                    tmp = "天津市";
                }
                else if (list[i].Key.Contains( "重庆市" ))
                {
                    tmp = "重庆市";
                }
                else if(list[i].Key.IndexOf( '区' ) > 0)
                {
                    tmp = list[i].Key.Substring( 0, list[i].Key.IndexOf( '区' ) + 1 );
                }

                if (string.IsNullOrEmpty( tmp )) continue;

                dataMy dataMy = shengfen.Find( m => m.Key == tmp );
                if(dataMy == null)
                {
                    shengfen.Add( new dataMy( tmp, list[i].Value ) );
                }
                else
                {
                    dataMy.Value += list[i].Value;
                }
            }

            shengfen.Sort( );
            shengfen.Reverse( );
            while (dataGridView2.RowCount < shengfen.Count)
            {
                dataGridView2.Rows.Add( );
            }

            while (dataGridView2.RowCount > shengfen.Count)
            {
                dataGridView2.Rows.RemoveAt( 0 );
            }

            // 赋值
            for (int i = 0; i < shengfen.Count; i++)
            {
                dataGridView2.Rows[i].Cells[0].Value = shengfen[i].Key;
                dataGridView2.Rows[i].Cells[1].Value = shengfen[i].Value.ToString( );
            }

        }

        private void Form1_FormClosing( object sender, FormClosingEventArgs e )
        {
            SaveData( );
        }
    }

    public class dataMy : IComparable<dataMy>
    {
        public dataMy( )
        {

        }

        public dataMy(string key,long value )
        {
            Key = key;
            Value = value;
        }


        public string Key { get; set; }
        public long Value { get; set; }

        public int CompareTo( dataMy other )
        {
            return Value.CompareTo( other.Value );
        }
    }
}
