﻿using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using ExtendedSerialPort;
using System.Threading;

namespace RobotInterface
{
    public partial class MainWindow : Window
    {
        Robot robot = new Robot();
        DispatcherTimer timerAffichage;
        private ReliableSerialPort serialPort1;

        public MainWindow()
        {
            InitializeComponent();

            serialPort1 = new ReliableSerialPort("COM5", 115200, Parity.None, 8, StopBits.One);
            serialPort1.DataReceived += SerialPort1_DataReceived;
            serialPort1.Open();

            timerAffichage = new DispatcherTimer();
            timerAffichage.Interval = new TimeSpan(0, 0, 0, 0, 40);
            timerAffichage.Tick += TimerAffichageTick;
            timerAffichage.Start();
        }

        private void TimerAffichageTick(object sender, EventArgs e)
        {
            while (robot.messageQueue.Count > 0)
            {
                Message m;

                bool success = robot.messageQueue.TryDequeue(out m);
                Console.WriteLine("Nb Message dans la queue : " + robot.messageQueue.Count);

                if (success)
                { 
                    string stringPayload = "";

                    for (int i = 0; i < m.PayloadLength; i++)
                    {
                        stringPayload += m.Payload[i].ToString("X2") + " ";
                    }

                    TextBoxData.Text = "Fonction = " + msgDecodedFunction + " LongeurPayload = " + msgDecodedPayloadLength + " Payload = " + stringPayload + " Checksum = " + isCkecksumOk;
                }
            }
        }

        private void buttonEnvoyer_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void SendMessage()
        {
            serialPort1.Write(textBoxEmission.Text);
            textBoxEmission.Text = null;
        }

        private void SerialPort1_DataReceived(object sender, DataReceivedArgs e)
        {
            for (int i = 0; i < e.Data.Length; i++)
            {
                DecodeMessage(e.Data[i]);
            }
        }

        private void buttonClear_Click(object sender, RoutedEventArgs e)
        {
            textBoxReception.Text = null;
        }

        private void buttonTest_Click(object sender, RoutedEventArgs e)
        {
            //byte[] byteList = new byte[20];
            //for (int i = 0; i < 20; i++)
            //{
            //    byteList[i] = (byte)(2 * i);
            //}
            //serialPort1.Write(byteList, 0, byteList.Count());

            string TestString = "AAA";
            byte[] array = Encoding.ASCII.GetBytes(TestString);
            UartEncodeAndSendMessage(0x0080, 3, array);
        }
    
        byte CalculateChecksum(int msgFunction, int msgPayloadLength, byte[] msgPayload)
        {
            byte cheksum = 0xFE;

            cheksum = (byte)(cheksum ^ msgFunction);
            cheksum = (byte)(cheksum ^ msgPayloadLength);

            for (int i = 0; i < msgPayloadLength; i++)
                cheksum ^= msgPayload[i];

            return cheksum;
        }

       void UartEncodeAndSendMessage(ushort msgFunction, ushort msgPayloadLength, byte[] msgPayload)
        {
            int i = 0, j = 0;
            byte[] msg = new byte[ 6 + msgPayloadLength];

            msg[i++] = 0xFE;

            msg[i++] = (byte)(msgFunction >> 8);
            msg[i++] = (byte)msgFunction;

            msg[i++] = (byte)(msgPayloadLength >> 8);
            msg[i++] = (byte)msgPayloadLength;

            for (j = 0; j < msgPayloadLength; j++)
                msg[i++] = msgPayload[j];

            msg[i++] = CalculateChecksum(msgFunction, msgPayloadLength, msgPayload);

            serialPort1.Write(msg, 0, msg.Length);
        }

        public enum StateReception
        {
            Waiting,
            FunctionMSB,
            FunctionLSB,
            PayloadLengthMSB,
            PayloadLengthLSB,
            Payload,
            CheckSum
        }

        //Definitions
        StateReception rcvState = StateReception.Waiting;

        byte[] msgDecodedPayload;
        byte msgDecodedChecksum;
        byte msgCalculatedChecksum;

        UInt16 msgDecodedFunction = 0;
        UInt16 msgDecodedPayloadLength = 0;
        int msgDecodedPayloadIndex = 0;
        int isCkecksumOk = -1;

        bool decodedFlag = false;

        
        private void DecodeMessage(byte c)
        {
            Console.Write("0x" + c.ToString("X2") + " ");
            switch (rcvState)
            {                
                case StateReception.Waiting:
                    if (c == 0xFE)
                    {
                        rcvState = StateReception.FunctionMSB;
                        decodedFlag = false;
                    }
                    break;

                case StateReception.FunctionMSB:
                    msgDecodedFunction = (UInt16)(c<<8);
                    rcvState = StateReception.FunctionLSB;
                    break;

                case StateReception.FunctionLSB:
                    msgDecodedFunction += c;
                    rcvState = StateReception.PayloadLengthMSB;
                    break;

                case StateReception.PayloadLengthMSB:
                    msgDecodedPayloadLength = (UInt16)(c<<8);
                    rcvState = StateReception.PayloadLengthLSB;
                    break;

                case StateReception.PayloadLengthLSB:
                    msgDecodedPayloadLength += c;
                    if (msgDecodedPayloadLength == 0)
                    {
                        rcvState = StateReception.CheckSum;
                    }
                    else if (msgDecodedPayloadLength < 1024)
                    {
                        msgDecodedPayload = new byte[msgDecodedPayloadLength];
                        msgDecodedPayloadIndex = 0;
                        rcvState = StateReception.Payload;
                    }
                    else
                    {
                        rcvState = StateReception.Waiting;
                    }
                    break;

                case StateReception.Payload:
                    msgDecodedPayload[msgDecodedPayloadIndex++] = c;
                    if (msgDecodedPayloadIndex >= msgDecodedPayloadLength)
                        rcvState = StateReception.CheckSum;                   
                    break;

                case StateReception.CheckSum:                    
                   msgDecodedChecksum = c;
                   msgCalculatedChecksum = CalculateChecksum(msgDecodedFunction, msgDecodedPayloadLength, msgDecodedPayload);
                    //decodedFlag = true;
                    Console.WriteLine("CHECKSUM : " + msgCalculatedChecksum.ToString("X2"));
                    if (msgDecodedChecksum == msgCalculatedChecksum)
                    {
                        robot.messageQueue.Enqueue(new Message(msgDecodedFunction, msgDecodedPayloadLength, msgDecodedPayload));
                    }
                    else
                    {
                        Console.WriteLine("Wrong Message Checksum");
                    }
                    rcvState = StateReception.Waiting;
                    break;

                default:
                    rcvState = StateReception.Waiting;
                    break;
            }
        }
    }
}
 

