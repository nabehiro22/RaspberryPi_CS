using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TCPServer
{
	class Server
	{
		/// <summary>
		/// ServerのIPアドレス
		/// </summary>
		private IPAddress IP;

		/// <summary>
		/// TCPポートがオープンしているか否かの判定
		/// </summary>
		private bool isOpen = false;

		/// <summary>
		/// TCPポートがオープンしているか否かの判定
		/// </summary>
		internal bool IsOpen
		{
			get { return isOpen; }
		}

		/// <summary>
		/// TCPサーバのソケット
		/// </summary>
		private Socket Sock;

		/// <summary>
		/// 受信バッファサイズ
		/// </summary>
		private int bufferSize;

		/// <summary>
		/// 接続待機のイベント
		/// </summary>
		private readonly ManualResetEventSlim connectDone = new ManualResetEventSlim(false);

		/// <summary>
		/// クライアント一覧
		/// System.ServiceModelへの参照が必要
		/// </summary>
		private Socket ClientSockets;

		/// <summary>
		/// コンストラクタ
		/// </summary>
		internal Server()
		{
		}

		/// <summary>
		/// デストラクタで念のため閉じる処理
		/// </summary>
		~Server()
		{
			Close();
		}

		/// <summary>
		/// TCPポートのオープン
		/// </summary>
		/// <param name="ipAddress">TCPサーバのIPアドレス</param>
		/// <param name="port">TCPサーバが使用するポート番号</param>
		/// <param name="buffersize">受信バッファのサイズ</param>
		/// <returns></returns>
		internal bool Open(string ipAddress, int port, int buffersize)
		{
			// 既にオープン状態ならtrueを返す
			if (isOpen == true)
				return true;

			bufferSize = buffersize;
			connectDone.Set();
			// 指定されたIPアドレスが正しい値かチェック
			if (IPAddress.TryParse(ipAddress, out IPAddress result) == true)
			{
				IP = result;
			}
			else
			{
				return false;
			}
			
			Sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			Sock.Bind(new IPEndPoint(IP, port));
			Sock.Listen(1);

			isOpen = true;
			accept();

			return true;
		}

		/// <summary>
		/// 接続待機を別タスクで実行
		/// </summary>
		private void accept()
		{
			Task.Run(() =>
			{
				while (true)
				{
					connectDone.Reset();
					try
					{
						Sock.BeginAccept(new AsyncCallback(acceptCallback), Sock);
					}
					catch (ObjectDisposedException)
					{
						// オブジェクトが閉じられていれば終了
						break;
					}
					catch (Exception)
					{
						continue;
					}
					connectDone.Wait();
				}
			});
		}

		/// <summary>
		/// 接続受け付けのコールバックメソッド
		/// </summary>
		/// <param name="asyncResult"></param>
		private void acceptCallback(IAsyncResult asyncResult)
		{
			// StateObjectを作成しソケットを取得
			var state = new StateObject(bufferSize);
			try
			{
				state.ClientSocket = ((Socket)asyncResult.AsyncState).EndAccept(asyncResult);
			}
			catch (ObjectDisposedException)
			{
				return;
			}
			catch (Exception)
			{
				return;
			}

			// 接続中のクライアントを追加
			ClientSockets = state.ClientSocket;
			// 受信時のコードバック処理を設定
			state.ClientSocket.BeginReceive(state.Buffer, 0, bufferSize, 0, new AsyncCallback(readCallback), state);
		}

		/// <summary>
		/// TCP非同期受信が完了した時に呼び出されるメソッド
		/// これは接続してきたクライアント毎に生成される
		/// </summary>
		/// <param name="asyncResult"></param>
		private void readCallback(IAsyncResult asyncResult)
		{
			// StateObjectとクライアントソケットを取得
			var state = (StateObject)asyncResult.AsyncState;
			// クライアントソケットから受信データを取得
			try
			{
				var ReadSize = state.ClientSocket.EndReceive(asyncResult);
				if (ReadSize > 0)
				{
					// TCPで受信したデータは「state.Buffer」にある。
					var readstr = Encoding.GetEncoding("shift_jis").GetString(state.Buffer).TrimEnd('\0'); ;
					SendData(readstr);
				}
				else
				{
					// 受信サイズが0の場合は切断(相手が切断した)
					state.ClientSocket.Close();
					ClientSockets = null;
					// 接続待機スレッドが進行するようにシグナルをセット
					connectDone.Set();
				}
			}
			catch (SocketException)
			{
				// 強制的に切断された
				state.ClientSocket.Close();
				ClientSockets = null;
				// 接続待機スレッドが進行するようにシグナルをセット
				connectDone.Set();
			}
			catch (Exception)
			{
			}
			finally
			{
				// 受信時のコードバック処理を設定
				if(ClientSockets != null)
					state.ClientSocket.BeginReceive(state.Buffer, 0, bufferSize, 0, new AsyncCallback(readCallback), state);
			}
		}

		/// <summary>
		/// TCP送信
		/// </summary>
		/// <param name="sendString">送信文字列</param>
		internal void SendData(string sendString)
		{
			ClientSockets.BeginSend(Encoding.ASCII.GetBytes(sendString), 0, sendString.Length, 0,new AsyncCallback(writeCallback), ClientSockets);
		}


		/// <summary>
		/// TCP非同期送信が完了した時に呼び出されるメソッド
		/// </summary>
		/// <param name="asyncResult"></param>
		private void writeCallback(IAsyncResult asyncResult)
		{
			((Socket)asyncResult.AsyncState).EndSend(asyncResult);
		}

		/// <summary>
		/// ソケットを閉じる
		/// </summary>
		internal void Close()
		{
			if (ClientSockets != null)
			{
				ClientSockets.Shutdown(SocketShutdown.Both);
				ClientSockets.Close();
				ClientSockets = null;
			}
			if (Sock != null)
			{
				Sock.Close();
				Sock.Dispose();
				Sock = null;
			}
			isOpen = false;
		}

		/// <summary>
		/// Socketのハンドラと使用する入出力バッファ
		/// </summary>
		private class StateObject
		{

			internal Socket ClientSocket { get; set; }
			internal byte[] Buffer;

			internal StateObject(int buffersize)
			{
				Buffer = new byte[buffersize];
			}
		}
	}
}
