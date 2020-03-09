using System;
using System.Diagnostics;
using System.Net;
using System.Windows.Forms;
using Raspberry.IO.GeneralPurpose;
using TCPServer;

namespace RaspberryPi_CS
{
	public partial class Form1 : Form
	{
		private readonly InputPinConfiguration GPIO2 = null;
		private readonly InputPinConfiguration GPIO3 = null;
		private readonly InputPinConfiguration GPIO4 = null;
		private readonly GpioConnection connection = null;
		private readonly Server server = new Server();
		private bool isOutput = false;

		public Form1()
		{
			InitializeComponent();

			// 自分のIPアドレスを探す
			string ipaddress = string.Empty;
			IPHostEntry ipentry = Dns.GetHostEntry(Dns.GetHostName());
			foreach (IPAddress ip in ipentry.AddressList)
			{
				if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
				{
					ipaddress = ip.ToString();
					break;
				}
			}
			// TCサーバの起動
			_ = server.Open(ipaddress, 5000, 1024);

			// 起動時に一度入力チェック
			getIO();

			// ここで示すピン番号は定義されている名称
			GPIO2 = ProcessorPin.Pin2.Input().PullUp();
			GPIO3 = ProcessorPin.Pin3.Input().PullUp();
			GPIO4 = ProcessorPin.Pin4.Input().PullUp();
			connection = new GpioConnection(GPIO2, GPIO3, GPIO4);
			// 入力変化時のイベント
			connection.PinStatusChanged += (sender, eventArgs) =>
			{
				if (eventArgs.Configuration.Pin == ProcessorPin.Pin2)
				{
					checkBox1.Checked = eventArgs.Enabled;
				}
				if (eventArgs.Configuration.Pin == ProcessorPin.Pin3)
				{
					checkBox2.Checked = eventArgs.Enabled;
				}
				if (eventArgs.Configuration.Pin == ProcessorPin.Pin4)
				{
					checkBox3.Checked = eventArgs.Enabled;
				}
			};
		}

		/// <summary>
		/// 意図したタイミングでGPIOの入力をチェック
		/// </summary>
		private void getIO()
		{
			ProcessorPin p2 = ProcessorPin.Pin02;
			ProcessorPin p3 = ProcessorPin.Pin03;
			ProcessorPin p4 = ProcessorPin.Pin04;
			_ = p2.Input().PullUp();
			_ = p3.Input().PullUp();
			_ = p4.Input().PullUp();
			IGpioConnectionDriver drv = GpioConnectionSettings.DefaultDriver;
			drv.Allocate(p2, PinDirection.Input);
			drv.Allocate(p3, PinDirection.Input);
			drv.Allocate(p4, PinDirection.Input);
			checkBox1.Checked = drv.Read(p2);
			checkBox2.Checked = drv.Read(p3);
			checkBox3.Checked = drv.Read(p4);
		}

		/// <summary>
		/// Formを閉じる時に後始末
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Form1_FormClosed(object sender, FormClosedEventArgs e)
		{
			server.Close();
			connection.Remove(GPIO2);
			connection.Remove(GPIO3);
			connection.Remove(GPIO4);
			connection.Close();
		}

		/// <summary>
		/// シャットダウンボタンが押された
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void button1_Click(object sender, EventArgs e)
		{
			if (MessageBox.Show(this, "システムをシャットダウンします。\r\nよろしいですか？", "システムシャットダウン", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
			{
				_ = Process.Start(new ProcessStartInfo() { FileName = "sudo", Arguments = "shutdown -h now" });
			}
		}

		/// <summary>
		/// GPIO出力
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void button2_Click(object sender, EventArgs e)
		{
			ProcessorPin p14 = ProcessorPin.Pin14;
			_ = p14.Output();
			IGpioConnectionDriver drv = GpioConnectionSettings.DefaultDriver;
			drv.Allocate(p14, PinDirection.Output);
			drv.Write(p14, isOutput);
			isOutput = !isOutput;
		}
	}
}
