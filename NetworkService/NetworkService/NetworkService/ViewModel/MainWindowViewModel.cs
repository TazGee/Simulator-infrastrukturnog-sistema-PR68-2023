using NetworkService.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NetworkService.ViewModel
{
    public class MainWindowViewModel
    {
        public List<MeracPotrosnje> listaObjekata = new List<MeracPotrosnje>();

        public MainWindowViewModel()
        {
            createListener(); //Povezivanje sa serverskom aplikacijom
        }

        private void createListener()
        {
            var tcp = new TcpListener(IPAddress.Any, 25675);
            tcp.Start();

            var listeningThread = new Thread(() =>
            {
                while (true)
                {
                    var tcpClient = tcp.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(param =>
                    {
                        //Prijem poruke
                        NetworkStream stream = tcpClient.GetStream();
                        string incomming;
                        byte[] bytes = new byte[1024];
                        int i = stream.Read(bytes, 0, bytes.Length);

                        //Primljena poruka je sacuvana u incomming stringu
                        incomming = System.Text.Encoding.ASCII.GetString(bytes, 0, i);

                        //Ukoliko je primljena poruka pitanje koliko objekata ima u sistemu -> odgovor
                        if (incomming.Equals("Need object count"))
                        {
                            //Response
                            /* Umesto sto se ovde salje count.ToString(), potrebno je poslati 
                             * duzinu liste koja sadrzi sve objekte pod monitoringom, odnosno
                             * njihov ukupan broj (NE BROJATI OD NULE, VEC POSLATI UKUPAN BROJ)
                             * */
                            Byte[] data = System.Text.Encoding.ASCII.GetBytes(listaObjekata.Count.ToString());
                            stream.Write(data, 0, data.Length);
                        }
                        else
                        {
                            //U suprotnom, server je poslao promenu stanja nekog objekta u sistemu
                            Console.WriteLine(incomming);

                            //################ IMPLEMENTACIJA ####################
                            // Obraditi poruku kako bi se dobile informacije o izmeni
                            int id;
                            double measure;
                            (id, measure) = ProcessMeasurementMessage(incomming);

                            // Azuriranje potrebnih stvari u aplikaciji
                            if (id != 0 && measure != 0)
                            {
                                WriteMeasurementLog(id, measure);
                            }
                        }
                    }, null);
                }
            });

            listeningThread.IsBackground = true;
            listeningThread.Start();
        }
        private (int, double) ProcessMeasurementMessage(string message)
        {
            string[] messageParts = message.Split(':');

            if (messageParts.Length != 2)
            {
                return (0, 0);
            }

            string entityPart = messageParts[0];
            string valuePart = messageParts[1];

            string[] entityParts = entityPart.Split('_');

            if (entityParts.Length != 2)
            {
                return (0, 0);
            }

            int entityIndex = int.Parse(entityParts[1]);
            double measuredValue = double.Parse(valuePart, System.Globalization.CultureInfo.InvariantCulture);

            return (entityIndex, measuredValue);
        }

        private void WriteMeasurementLog(int entityId, double measure)
        {
            string logsFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

            if (!Directory.Exists(logsFolderPath))
            {
                Directory.CreateDirectory(logsFolderPath);
            }

            string logFilePath = Path.Combine(logsFolderPath, "measurements_log.txt");
            string status = measure >= 0.34 && measure <= 2.73 ? "VALID" : "INVALID";

            string logLine =    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                                " | Entity: Entitet_" + entityId +
                                " | Value: " + measure.ToString("F2", CultureInfo.InvariantCulture) + " kWh" +
                                " | Status: " + status +
                                Environment.NewLine;
        }
    }
}
