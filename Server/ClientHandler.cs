/* ------------------------------------------------------------------------------------------------
 * Project Chess Duelist 
 * Author: Hye Mi Byun
 * Creation Date: July, 2015 
 * Description: ClientHandler class is for handling client requests
 *              Receive method must be handled in thread, so that a msg is sent immediately
 * ------------------------------------------------------------------------------------------------
 */
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.Data;
using MySql.Data.MySqlClient;

public class ClientHandler
{
    NetworkStream stream = null;
    TcpClient tcpClient;

    /*roomClassList: key = room number, value = room class
     *roomList: key = host id, value = room number
     */
    Hashtable clientList;
    Hashtable roomList;
    Hashtable roomClassList;

    RoomHandler room;

    string clientNo;
    public static int roomCounter;
    public static bool clientWhileFlag = true;

    String[] roomData;

    public void StartClient(TcpClient inClinetSocket, string clientNo,
        Hashtable clientLst, Hashtable roomList, Hashtable roomClassList)
    {
        this.tcpClient = inClinetSocket;
        this.clientList = clientLst;
        this.roomList = roomList;
        this.roomClassList = roomClassList;
        this.clientNo = clientNo;

        Thread clThread = new Thread(Receive);
        clThread.Start();
    }

    private void Receive()
    {
        int counter = 0;
        byte[] bytesFrom = new byte[1024];
        string dataFromClient = null;

        while (clientWhileFlag)
        {
            try
            {
                counter = counter + 1;

                //a NetworkStream that you can use to send and receive data
                stream = tcpClient.GetStream();
                stream.Read(bytesFrom, 0, bytesFrom.Length);

                dataFromClient = System.Text.Encoding.ASCII.GetString(bytesFrom);
                dataFromClient = dataFromClient.Substring(0, dataFromClient.IndexOf("$"));
                Console.WriteLine("\n<Receive> " + dataFromClient);
                if (dataFromClient.Contains("LoginReq"))
                {
                    //call a method accordingly
                    LoginAckEvent(dataFromClient);
                    //clears all buffers
                    stream.Flush();
                }
                else if (dataFromClient.Contains("RegisterReq"))
                {
                    RegisterAckEvent(dataFromClient);
                    stream.Flush();
                }
                else if (dataFromClient.Contains("RankReq"))
                {
                    RankAckEvent();
                    stream.Flush();
                }
                else if (dataFromClient.Contains("RoomListReq"))
                {
                    RoomListAckEvent(roomList);
                    stream.Flush();
                }
                else if (dataFromClient.Contains("CreateRoomReq"))
                {
                    CreateRoomAckEvent(dataFromClient, roomList);
                    stream.Flush();
                }
                else if (dataFromClient.Contains("JoinRoomReq"))
                {
                    JoinRoomAckEvent(dataFromClient, roomList);
                    stream.Flush();

                }
                else if (dataFromClient.Contains("DestroyRoomReq"))
                {
                    DestroyRoomAckEvent(dataFromClient, roomList, roomClassList);
                    stream.Flush();
                }
                else if (dataFromClient.Contains("LeaveRoomReq"))
                {
                    LeaveRoomAckEvent(dataFromClient, roomClassList);
                    stream.Flush();
                }
                else if (dataFromClient.Contains("ReadyReq"))
                {
                    ReadyAckEvent(dataFromClient, roomClassList);
                    stream.Flush();
                }
                else if (dataFromClient.Contains("StartReq"))
                {
                    StartAckEvent(dataFromClient, roomList, roomClassList);
                    stream.Flush();
                }
                else if (dataFromClient.Contains("MoveReq"))
                {
                    MoveAckEvent(dataFromClient, roomClassList);
                    stream.Flush();
                }
                else if (dataFromClient.Contains("PossiblePathReq")) {
                    PossiblePathAckEvent(dataFromClient, roomClassList);
                    stream.Flush();
                }
                else if (dataFromClient.Contains("StatusReq"))
                {
                    StatusAckEvent(roomClassList);
                    stream.Flush();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.WriteLine(clientNo + " Client is dead");
                break;
            }
        }
    }

    private void Send(string msg)
    {
        while (true)
        {
            try
            {
                stream = tcpClient.GetStream();
                //a TextWriter for writing characters to a stream in UTF-8 encoding 
                StreamWriter streamWriter = new StreamWriter(stream);

                Console.WriteLine("<Send> " + msg);
                streamWriter.WriteLine(msg);

                //clears all buffers
                streamWriter.Flush();
                stream.Flush();
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                break;
            }
        }
    }

    /* for a register event
     * get a uid and pw from a client,
     * and store them into a DB
     */
    private void RegisterAckEvent(string RegisterData)
    {
        string[] registerDataArray = RegisterData.Split(':');

        string strCon = "Server=127.0.0.1;port=3306;uid=root;pwd=1111;Database=score;";
        MySql.Data.MySqlClient.MySqlConnection con;
        try
        {
            Console.WriteLine("Connecting to MySQL...");
            con = new MySql.Data.MySqlClient.MySqlConnection();

            con.ConnectionString = strCon;
            con.Open();

            MySqlCommand cmdInsert = new MySqlCommand("INSERT INTO score(uid, pw) VALUES (@uid, @pw)", con);
            cmdInsert.Parameters.AddWithValue("@uid", registerDataArray[1]);
            cmdInsert.Parameters.AddWithValue("@pw", registerDataArray[2]);

            //to check whether a query is successful or not
            int check = Convert.ToInt32(cmdInsert.ExecuteNonQuery());
            switch (check)
            {
                case 1:
                    //if success(query), then send positive msg to a client 
                    Send(registerDataArray[1] + ":RegisterAck");
                    break;
                default:
                    //else send negative msg
                    Send("Try Again");
                    break;
            }
            con.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return;
        }
    }

    /* for an Update a rank event
     * access DB and send a returned list to a client 
     */
    private void RankAckEvent()
    {
        string rankData = "RankAck";
        string strCon = "Server=127.0.0.1;port=3306;uid=root;pwd=1111;Database=score;";
        MySqlConnection con = new MySqlConnection(strCon);
        con.Open();
        try
        {
            MySqlCommand cmd = con.CreateCommand();
            //a returned list must be sorted in descending order
            cmd.CommandText = "SELECT uid, winningRate, numOfWinGames, numOfLoseGames FROM score ORDER BY winningRate DESC, numOfWinGames DESC";

            MySqlDataReader dReader = cmd.ExecuteReader();
            DataTable dt = new DataTable();
            dt.Load(dReader);
            //on all tables' rows
            foreach (DataRow dr in dt.Rows)
            {
                //on all tables' columns
                foreach (DataColumn dc in dt.Columns)
                {
                    //check the null values
                    if (dr[dc] != null)
                    {
                        string field = dr[dc].ToString();
                        //just concatenate one by one
                        rankData = rankData + ":" + field;
                    }
                }
            }
            //MySqlDataReader and MySqlConnection must be closed
            dReader.Close();
            con.Close();

            //send the returned data to a client
            Send(rankData);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return;
        }
    }
    /* for a login event
     * access DB and return true if a requested uid and id is in it or not
     * and finally send a result to a client
     */
    private void LoginAckEvent(string LoginData)
    {
        string[] loginDataArray = LoginData.Split(':');

        string strCon = "Server=127.0.0.1;port=3306;uid=root;pwd=1111;Database=score;";
        MySqlConnection con = new MySqlConnection(strCon);
        con.Open();

        try
        {
            MySqlCommand cmdLogin = con.CreateCommand();

            cmdLogin.CommandText = "SELECT uid FROM score WHERE uid=@Uid and pw=@Pw;";
            cmdLogin.Parameters.AddWithValue("@Uid", loginDataArray[1]);
            cmdLogin.Parameters.AddWithValue("@Pw", loginDataArray[2]);

            MySqlDataReader dReader = cmdLogin.ExecuteReader();

            //if success(query), then send positive msg to a client, else send negative msg
            if (dReader.Read()) Send(loginDataArray[1] + ":LoginAck");
            else Send("LoginErrorNoti");

            //MySqlDataReader and MySqlConnection must be closed
            dReader.Close();
            con.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return;
        }
    }

    private void RoomListAckEvent(Hashtable roomList)
    {
        string msg = "";
        try
        {
            foreach (DictionaryEntry s in roomList)
            {
                msg = msg + s.Value + ":" + s.Key + ":";
            }
            //if there is no more room then send a msg to a client
            Send(msg + "RoomListAck");
        }
        //Handle Exception
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return;
        }
    }

    private void CreateRoomAckEvent(string reqData, Hashtable roomList)
    {
        //Send(s, validUid+":"+roomName+":CreateRoomReq", 1000);  
        String[] roomData = reqData.Split(':');
        try
        {
            /* example of roomList 
            * key: "fc1202" value:"title"
            * in this case, fc1202 is the user creating a room called "title"
            */
            roomList.Add(roomData[0], roomData[1]);

            Console.WriteLine("We have " + roomList.Count.ToString() + " Rooms"
                + "\n" + roomData[0] + " creates room no." + roomData[1]);

            //make a room class
            room = new RoomHandler();
            room.StartRoom(tcpClient);
            room.hostName = roomData[0];

            /* example of roomClassList 
            * key: "title" value: room that has been created by clicking a create room button
            */
            roomClassList.Add(roomData[1], room);

            Send(roomData[1] + ":CreateRoomAck");
        }
        //Handle Exception
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return;
        }
    }

    private void JoinRoomAckEvent(string reqData, Hashtable roomList)
    {
        //Send(s, validUid + ":" + roomName + ":JoinRoomReq", 1000); 
        roomData = reqData.Split(':');

        try
        {
            /*roomClassList: key = room number, value = room class
             *roomList: key = host id, value = room number
             */
            room = (RoomHandler)roomClassList[roomData[1]];

            //only if there is no guest in a room, a player can join
            if (!room.isFull)
            {
                room.guestStartRoom(tcpClient);
                room.guestName = roomData[0];
                Console.WriteLine(room.guestName);

                //indicates the room is already full
                room.isFull = true;
                
                //look for host id, and send it to a client
                foreach (DictionaryEntry entry in roomList)
                {
                    if (entry.Value.ToString() == roomData[1])
                    {
                        string result = entry.Key.ToString();
                        room.SendToClient(result + ":" + roomData[0] + ":JoinRoomAck");
                        room.isReady = false;
                    }
                }
            }
            else
            {
                //if a room is full, a player cannot join a room 
                Send("FullRoomNoti");
            }
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    private void LeaveRoomAckEvent(string reqData, Hashtable roomClassList)
    {
        //reqData =roomNo+":"+validUid+":LeaveRoom"
        roomData = reqData.Split(':');
        try
        {
            room = (RoomHandler)roomClassList[roomData[0]];
            room.ResetIsFull();
            room.SendToClient(roomData[0] + ":LeaveRoomAck");
        }
        //Handle Exception
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return;
        }
    }

    //only the host of a room can destroy a room
    private void DestroyRoomAckEvent(string reqData, Hashtable roomList, Hashtable roomClassList)
    {
        //reqData = roomNo+":"+validUid+":DestroyRoom"
        roomData = reqData.Split(':');

        /*roomClassList: key = room number, value = room class
         *roomList: key = host id, value = room number
         */
        try
        {
            room = (RoomHandler)roomClassList[roomData[0]];
            room.SendToClient("DestroyRoomAck");

            //remove everything related to the current room
            roomClassList.Remove(roomData[0]);
            roomList.Remove(roomData[1]);
        }
        //Handle Exception
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return;
        }
    }
    private void StartAckEvent(string reqData, Hashtable roomList, Hashtable roomClassList)
    {
        //"StartReq"+roomNo+validUid
        roomData = reqData.Split(':');

        try
        {
            /*roomClassList: key = room title, value = room class
             *roomList: key = host id, value = room title
             */
            room = (RoomHandler)roomClassList[roomData[1]];
            if (room.isFull == false) Send("GuestNotArriveNoti");
            else if (room.isReady == true)
            {
                room.isStart = true;

                //call start game function to allocate a game class instance
                room.startGame();
                room.SendToClient(roomData[1] + ":StartAck");
                //remove a room number from a list, so that it doesn't appear on the lobby
                roomList.Remove(roomData[2]);
            }
            else Send("GuestNotReadyNoti");
        }
        //Handle Exception
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return;
        }
    }

    private void ReadyAckEvent(string reqData, Hashtable roomClassList)
    {
        //Send(s, "ReadyReq:"+roomNo, 1000);	
        roomData = reqData.Split(':');
        try
        {
            /* roomClassList: key = room number, value = room class
             * find a right room for received room number from a client
             * and send acknowledgement
             */
            room = (RoomHandler)roomClassList[roomData[1]];
            room.isReady = !room.isReady;
            if (room.isReady == true)
                Send("ReadyAck");
            else
                Send("CancelReadyAck");
            return;
        }
        //Handle Exception
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return;
        }
    }

    // send the list of possible moves of the clicked
    private void PossiblePathAckEvent(string reqData, Hashtable roomClassList)
    {
        /* isHost+":"+roomNo+":"+"PossibleMoveReq"+":"+myPieceName+":"+myStartPos
         */ 
         List<int> PossibleMoveList = new List<int>();
         roomData = reqData.Split(':');
        try
        {   /* roomClassList: key = room number, value = room class
             * find a right room for received room number from a client
             * and send acknowledgement
             */
            room = (RoomHandler)roomClassList[roomData[1]];
            if (room.isStart == true)
            {
                /* only startPos and finalPos are needed to proceed the movement
                 * since the server knows a board status
                 */
                PossibleMoveList = room.getPossibleMove(int.Parse(roomData[4]));

                /* send to the client only if PossibleMoveList contains an integer value
                 * meaning the clicked piece can be moved
                 */ 
                if (PossibleMoveList != null)
                {
                    Send(string.Join(":", PossibleMoveList) + ":" + roomData[4] + ":PossiblePathAck");
                }
            }
            else Send("NotStartNoti");
        }
        //Handle Exception
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return;
        }
    }

    //
    private void MoveAckEvent(string reqData, Hashtable roomClassList)
    {
        /* isHost+":"+roomNo+":"+"MoveReq"+":"+myPieceName+":"+myStartPos+":"+myFinalPos
         * isHost argument is needed for define who won the game and who lost the game
         */
        roomData = reqData.Split(':');
        try
        {   /* roomClassList: key = room number, value = room class
             * find a right room for the received room number
             * and send acknowledgement
             */
            room = (RoomHandler)roomClassList[roomData[1]];
            if (room.isStart == true)
            {                
                /* only startPos and finalPos are needed to proceed the movement
                 * since the server knows a board status
                 */ 
                bool isValidMove = room.getMovePos(int.Parse(roomData[4]), int.Parse(roomData[5]));
                if (isValidMove)
                {
                    room.setTurn(roomData[0]);
                    room.SendToClient(roomData[0] + ":" + roomData[1] + ":MoveAck:" + roomData[3] + ":" + roomData[4] + ":" + room.RealFinalPos);
                    
                    /* any special move? like enpassant and castling?
                     * These two must be notified separately to the clients
                     */
                    //EnPassant
                    if (room.Enpassant != null)
                    {
                        room.SendToClient(room.Enpassant.ToString() + ":EnPassant"); 
                        room.Enpassant = null;
                    }
                    //Castling
                    if (room.StartPosCastling != null)
                    {
                        room.SendToClient(room.StartPosCastling.ToString() + ":"+ room.FinalPosCastling.ToString() + ":Castling");
                        room.StartPosCastling = null;
                        room.FinalPosCastling = null;
                    }
                    
                    /* then send the status of the game along with the roomData[0](isHost value).
                     * if either checkmate, draw then notifies the clients to end the game
                     * if check, notifies the client to show 'check' pop-up window
                     */
                    switch (room.getStatus())
                    {
                        case "Check":
                        case "Stalemate":
                        case "Normal":
                        case "DrawByFiftyMove":
                        case "DrawByLackOfMaterial":
                        case "DrawByThreefold":
                            room.SendToClient(roomData[0] +":"+ room.getStatus());
                            break;
                        case "Checkmate":
                            room.SendToClient(roomData[0] +":"+ room.getStatus());
                            //if the move was made by the host, then do the number of the win games += 1 to host
                            if (roomData[0] == "True") 
                            { 
                                UpdateRankIfWin(room.hostName);
                                UpdateRankIfLose(room.guestName);
                            }
                            //if the move was made by the guset, then do the number of the win games += 1 to guest
                            else if (roomData[0] == "False")
                            {
                                UpdateRankIfWin(room.guestName);
                                UpdateRankIfLose(room.hostName);                            
                            }
                            break;
                        default:
                            break;
                    }
                }
                else {
                    //Send("NotMoveNoti");
                }
            }
            //room.SendToClient(reqData);            
            else Send("NotStartNoti");
        }
        //Handle Exception
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return;
        }
    }

    //send the current status of captured piece
    private void StatusAckEvent(Hashtable roomClassList)
    {
        try
        {   /* roomClassList: key = room number, value = room class
             * find a right room for received room number from a client
             * and send acknowledgement
             */
            room = (RoomHandler)roomClassList[roomData[1]];
            Send(room.getCapturedPieceStatus()+":StatusAck");
        }

        //Handle Exception
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return;
        }
    }

    //Update the rank of a loser when the game ends
    private void UpdateRankIfLose(string userName)
    {
        string strCon = "Server=127.0.0.1;port=3306;uid=root;pwd=1111;Database=score;";

        string updateRankData = "";
        string[] updateRankArray;

        int numWin;
        int numLose;
        float winRate;

        MySqlConnection con = new MySqlConnection(strCon);
        con.Open();

        try
        {
            MySqlCommand cmd = con.CreateCommand();

            //ask for user's data to update the rank            
            cmd.CommandText = "SELECT numOfWinGames, numOfLoseGames FROM score.score Where uid = @userId";
            cmd.Parameters.AddWithValue("@userId", userName);

            MySqlDataReader dReader = cmd.ExecuteReader();
            DataTable dt = new DataTable();
            dt.Load(dReader);

            //on all tables' rows
            foreach (DataRow dr in dt.Rows)
            {
                //on all tables' columns
                foreach (DataColumn dc in dt.Columns)
                {
                    //check the null values
                    if (dr[dc] != null)
                    {
                        string field = dr[dc].ToString();
                        //just concatenate one by one
                        updateRankData = updateRankData + ":" + field;
                    }
                }
            }

            updateRankArray = updateRankData.Split(':');

            numWin = int.Parse(updateRankArray[1]);
            numLose = int.Parse(updateRankArray[2]);
            //if lose the game, add 1 to numLose
            numLose += 1;
            winRate = (float)numWin / (numWin + numLose);
            winRate *= 100f;

            //update the database UPDATE `score`.`score` SET `uid`='test' WHERE  `id`=1;
            cmd = con.CreateCommand();
            cmd.CommandText = "UPDATE score.score SET winningRate = @winRate, numOfLoseGames = @loseGame WHERE uid = @userId";
            cmd.Parameters.AddWithValue("@userId", userName);
            cmd.Parameters.AddWithValue("@loseGame", numLose);
            cmd.Parameters.AddWithValue("@winRate", winRate);
            cmd.ExecuteNonQuery();

            //MySqlDataReader and MySqlConnection must be closed
            dReader.Close();
            con.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return;
        }
    }

    //Update the rank of a winner when the game ends
    private void UpdateRankIfWin(string userName)
    {
        string strCon = "Server=127.0.0.1;port=3306;uid=root;pwd=1111;Database=score;";

        string updateRankData = "";
        string[] updateRankArray;

        int numWin;
        int numLose;
        float winRate;

        MySqlConnection con = new MySqlConnection(strCon);
        con.Open();

        try
        {
            MySqlCommand cmd = con.CreateCommand();

            //ask for user's data to update the rank            
            cmd.CommandText = "SELECT numOfWinGames, numOfLoseGames FROM score Where uid = @userId";
            cmd.Parameters.AddWithValue("@userId", userName);

            MySqlDataReader dReader = cmd.ExecuteReader();
            DataTable dt = new DataTable();
            dt.Load(dReader);

            //on all tables' rows
            foreach (DataRow dr in dt.Rows)
            {
                //on all tables' columns
                foreach (DataColumn dc in dt.Columns)
                {
                    //check the null values
                    if (dr[dc] != null)
                    {
                        string field = dr[dc].ToString();
                        //just concatenate one by one
                        updateRankData = updateRankData + ":" + field;
                    }
                }
            }

            updateRankArray = updateRankData.Split(':');

            foreach (string s in updateRankArray)
            {
                Console.Write(" " + s);
            }

            numWin = int.Parse(updateRankArray[1]);
            numLose = int.Parse(updateRankArray[2]);
            //if won the game, add 1 to numLose
            numWin += 1;
            winRate = (float)numWin / (numWin + numLose);
            winRate *= 100f;

            //update the database
            cmd = con.CreateCommand();
            cmd.CommandText = "UPDATE score.score SET winningRate = @winRate, numOfWinGames = @winGame WHERE uid = @userId";
            cmd.Parameters.AddWithValue("@userId", userName);
            cmd.Parameters.AddWithValue("@winGame", numWin);
            cmd.Parameters.AddWithValue("@winRate", winRate);
            cmd.ExecuteNonQuery();

            //MySqlDataReader and MySqlConnection must be closed
            dReader.Close();
            con.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return;
        }
    }
}
