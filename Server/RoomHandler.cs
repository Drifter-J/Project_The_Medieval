/* ------------------------------------------------------------------------------------------------
 * Project Chess Duelist 
 * Author: Hye Mi Byun
 * Creation Date: July, 2015 
 * Description: RoomHandler class is for buidling a P2P(Host and Guest) Communication Environment
 * ------------------------------------------------------------------------------------------------
 */
using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using MySql.Data.MySqlClient;

public class RoomHandler
{
    public TcpClient hostClient;
    public string hostName;
    public TcpClient guestClient;
    public string guestName;

    public bool isReady = false;
    public bool isStart = false;
    public bool isFull = false;

    private Game game;
    private int realFinalPos;

    public int RealFinalPos
    {
        get { return realFinalPos; }
    }
    
    //set host's tcpClient
    public void StartRoom(TcpClient hostClient)
    {        
        //client is a host
        this.hostClient = hostClient;
    }

    //set guest's tcpClient
    public void guestStartRoom(TcpClient guestClient)
    {
        if (hostClient == null) {
            Console.WriteLine("Unaccessible Room");
        }
        if (isFull == false)
        {
            this.guestClient = guestClient;        
        }
    } 

    //set isFull as false
    public void ResetIsFull() {
        isFull = false;
    }

    //using for send a message from a client
    public void SendToClient(string msg) {
        SendToBoth(msg);
    }

    private void SendMsg(TcpClient Client, string msg)
    {
        NetworkStream stream;
        try{
            stream = Client.GetStream();
            StreamWriter streamWriter = new StreamWriter(stream);
            streamWriter.WriteLine(msg);
            streamWriter.Flush();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }        
    }

    private void SendToBoth(string msg)
    {
        //when a guest enters a room create thread to handle two clients' request
        try
        {
            if (guestClient != null)
            {
                SendMsg(guestClient, msg);
                SendMsg(hostClient, msg);
                Console.WriteLine("<Send Both> " + msg);
            }
            else { 
                SendMsg(hostClient, msg); 
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }        
    }

    //start a new game
    public void startGame() {
        game = new Game();
        Board board = Board.SetStart();
        game.CurrentBoard = board;
    }

    public List<int> getPossibleMove(int startPos)
    {
        List<int> PossibleMoveList = new List<int>();
        if (game != null)
        {
            PossibleMoveList = game.GetPossibleMove(startPos);
            return PossibleMoveList;
        }
        else {
            return null;
        }
    }

    // move the piece from startPos to finalPos
    public bool getMovePos(int startPos, int finalPos)
    {       
        /* if move from startPos to finalPos is a valid movement
         * then return true, else return false
         */
        bool canGetMove = game.Move(game.GetMove(startPos, finalPos));
        realFinalPos = game.RealFinalPos;
        game.CurrentBoard.callTempFunc();
        return canGetMove;
        //game.callDictFunc();
        //game.CurrentBoard.callTempFunc();
    }

    /* get the status of the game
     * if either checkmate, draw then notifies the clients to end the game
     * if check, notifies the client to show 'check' pop-up window
     */
    public string getStatus() {
        return game.GetStatus().ToString();
    }

    public void setTurn(string isHost) {
        //if the client is the host(blue)
        if (isHost.Equals("True"))
        {
            BoardStatus currentBoard = game.CurrentBoard.getStatus();
            currentBoard.IsBlueTurn = true;
        }
        if (isHost.Equals("False"))
        {
            BoardStatus currentBoard = game.CurrentBoard.getStatus();
            currentBoard.IsBlueTurn = false;
        }
    }

    public string getCapturedPieceStatus() {
        return game.CurrentBoard.getCapturedPieceStat();
    }

    public int? Enpassant {
        get { return game.CurrentBoard.CapturedPieceByEnPassant; }
        set { game.CurrentBoard.CapturedPieceByEnPassant = value; }
    }

    public int? StartPosCastling
    {
        get { return game.CurrentBoard.MovedPieceByCastlingStartPos;}
        set { game.CurrentBoard.MovedPieceByCastlingStartPos = value; }
    }

    public int? FinalPosCastling
    {
        get { return game.CurrentBoard.MovedPieceByCastlingFinalPos; }
        set { game.CurrentBoard.MovedPieceByCastlingFinalPos = value; } 
    }
}

