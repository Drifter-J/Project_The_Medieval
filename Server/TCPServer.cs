/* ------------------------------------------------------------------------------------------------
 * Project Chess Duelist 
 * Author: Hye Mi Byun
 * Creation Date: July, 2015 
 * Description: TCPServer class is for handling client connections
 * ------------------------------------------------------------------------------------------------
 */
using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.Data;
using MySql.Data.MySqlClient;

class TCPServer
{
    /* use Hashtable b/c insert, delete, lookup are fast,
     * and it generically prevents duplicates
     * - no user id can be logged in twice
     * - no room can be made twice for one user id
     */ 
    public static Hashtable roomClassList = new Hashtable();
    public static Hashtable clientList = new Hashtable();
    public static Hashtable roomList = new Hashtable();
    
    public static void Main()
    {
        TcpListener tcpListener = null;
        TcpClient tcpClient = null;

        try
        {
            int counter = 0;
            //create a TcpListener
            tcpListener = new TcpListener(5001);

            Console.WriteLine("Start Server");
            tcpListener.Start();

            while (true)
            {
                /*Counter is only increasing, not decreasing 
                 * to avoid the same client Number among clients
                 */ 
                counter += 1;

                /* when a client request for a connection, accept it 
                 * and add the tcpClient to the hashtable above
                 */
                tcpClient = tcpListener.AcceptTcpClient();
                Console.WriteLine("Accept Connection Client No." + Convert.ToString(counter));
                clientList.Add(Convert.ToString(counter), tcpClient);

                //create a clientHandler
                ClientHandler client = new ClientHandler();
                client.StartClient(tcpClient, Convert.ToString(counter), clientList, roomList, roomClassList);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
        finally
        {
            //a tcpClient will be closed after all other error processing has occurred
            tcpClient.Close();
        }
    }

    //for notice and chatting
    public static void Broadcast(string str)
    {
        foreach (DictionaryEntry Item in clientList)
        {
            TcpClient TcpBroadCast;
            try
            {
                TcpBroadCast = (TcpClient)Item.Value;

                //a NetworkStream that you can use to send and receive data
                NetworkStream broadCastStream = TcpBroadCast.GetStream();
                //a TextWriter for writing characters to a stream in UTF-8 encoding 
                StreamWriter streamWriter = new StreamWriter(broadCastStream);

                Console.WriteLine("Broadcast to all: " + str);
                streamWriter.WriteLine(str);

                //clears all buffers
                streamWriter.Flush();
                broadCastStream.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                continue;
            }
        }
    }
}