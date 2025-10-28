using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using csDronLink;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.IO;
using System.Linq;


namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        Dron miDron = new Dron();

        // STREAMING
        private VideoCapture capPC;
        private VideoCapture capDron;
        private bool running = false;

        // TCP SERVER
        private TcpListener listener;
        private bool serverRunning = false;

        public Form1()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;

            // 🔹 Aquí llamamos manualmente al método de carga
            Form1_Load(this, EventArgs.Empty);
        }

        // ==========================
        //     INICIALIZACIÓN
        // ==========================
        private void Form1_Load(object sender, EventArgs e)
        {
            IniciarServidorTCP();   //Gestos
            IniciarServidorVideo();   //Video
            IniciarScriptPython();  // Ejecutar el script Python

        }

        // ==========================
        //     TELEMETRÍA
        // ==========================
        private void ProcesarTelemetria(byte id, List<(string nombre, float valor)> telemetria)
        {
            foreach (var t in telemetria)
            {
                if (t.nombre == "Alt")
                {
                    altLbl.Text = t.valor.ToString();
                    break;
                }
            }
        }

        // ==========================
        //     BOTONES MANUALES
        // ==========================
        private void button1_Click_1(object sender, EventArgs e)
        {
            miDron.Conectar("simulacion");
            miDron.EnviarDatosTelemetria(ProcesarTelemetria);
        }

        private void EnAire(byte id, object param)
        {
            button2.BackColor = Color.Green;
            button2.ForeColor = Color.White;
            button2.Text = (string)param;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            miDron.Despegar(20, bloquear: false, f: EnAire, param: "Volando");
            button2.BackColor = Color.Yellow;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            miDron.Aterrizar(bloquear: false);
        }

        private bool sistemaActivo = false;


        /* private void button5_Click(object sender, EventArgs e)
         {
             miDron.CambiarHeading(90, bloquear: false);
         }

         private void button6_Click(object sender, EventArgs e)
         {
             miDron.CambiarHeading(270, bloquear: false);
         }

         private void button7_Click(object sender, EventArgs e)
         {
             miDron.Mover("Forward", 10, bloquear: false);
         }
        */

        // ==========================
        //     TCP SERVER GESTOS
        // ==========================
        private void IniciarServidorTCP()
        {
            if (serverRunning)
            {
                listBox1.Items.Add("⚠️ El servidor TCP ya está en ejecución.");
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    int puerto = 5005;
                    listener = new TcpListener(IPAddress.Parse("127.0.0.1"), puerto);
                    listener.Start();
                    serverRunning = true;

                    listBox1.Items.Add($"Servidor TCP iniciado en puerto {puerto}");

                    while (serverRunning)
                    {
                        TcpClient client = listener.AcceptTcpClient();
                        listBox1.Items.Add("Cliente conectado desde Python.");

                        NetworkStream stream = client.GetStream();
                        byte[] buffer = new byte[1024];

                        StringBuilder sb = new StringBuilder();

                        while (client.Connected)
                        {
                            int bytesRead = stream.Read(buffer, 0, buffer.Length);
                            if (bytesRead == 0) break;

                            string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            sb.Append(data);

                            // Procesar cada línea completa (cada gesto termina con '\n')
                            while (sb.ToString().Contains("\n"))
                            {
                                string line = sb.ToString();
                                int index = line.IndexOf('\n');
                                string mensaje = line.Substring(0, index).Trim();
                                sb.Remove(0, index + 1);

                                if (!string.IsNullOrWhiteSpace(mensaje))
                                {
                                    listBox1.Items.Add($"Gesto recibido: {mensaje}");
                                    EjecutarAccionPorGesto(mensaje);
                                }
                            }
                        }


                        client.Close();
                        listBox1.Items.Add("Cliente desconectado.");
                    }
                }
                catch (Exception ex)
                {
                    listBox1.Items.Add($"Error en servidor TCP: {ex.Message}");
                }
            });
        }

        // ==========================
        //     ARRANCAR SCRIPT PYTHON
        // ==========================
        private System.Diagnostics.Process pythonProcess;

        private void IniciarScriptPython()
        {
            try
            {
                string pythonExe = @"C:\Users\CARLA\AppData\Local\Programs\Python\Python310\python.exe";
                string scriptPath = @"C:\Users\CARLA\Desktop\UNIVERSITAT\TFG\TFG-Reconocimiento_de_gestos\WindowsFormsApp1\WindowsFormsApp1\detectar_mano.py";

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = $"\"{scriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                pythonProcess = new System.Diagnostics.Process { StartInfo = psi };
                pythonProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        // Filtrar mensajes técnicos irrelevantes
                        string line = e.Data.Trim();
                        if (line.StartsWith("W0000") || line.Contains("inference_feedback_manager"))
                            return; // Ignorar avisos internos de MediaPipe
                        if (line.Contains("INFO") || line.Contains("DEBUG") ||
                            line.Contains("MediaPipe") || line.Contains("TensorFlow"))
                            return; // Ignorar mensajes del framework

                        // Mostrar solo mensajes útiles
                        listBox1.Items.Add($"[Python]: {line}");
                    }
                };

                pythonProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        string line = e.Data.Trim();
                        // Ignorar advertencias y logs ruidosos
                        if (line.Contains("TensorFlow") || line.Contains("XNNPACK") ||
                            line.Contains("WARNING") || line.Contains("DeprecationWarning") ||
                            line.StartsWith("W0000") || line.Contains("inference_feedback_manager"))
                            return;

                        // Mostrar solo errores relevantes
                        listBox1.Items.Add($"⚠️ {line}");
                    }
                };


                pythonProcess.Start();
                pythonProcess.BeginOutputReadLine();
                pythonProcess.BeginErrorReadLine();

                listBox1.Items.Add("✅ Script Python iniciado correctamente.");
            }
            catch (Exception ex)
            {
                listBox1.Items.Add($"❌ Error al iniciar script Python: {ex.Message}");
            }
        }

        // ==========================
        //     VIDEO DESDE PYTHON
        // ==========================
        private TcpListener videoListener;
        private bool videoServerRunning = false;

        private void IniciarServidorVideo()
{
    Task.Run(() =>
    {
        try
        {
            int puerto = 5006; // debe coincidir con Python
            videoListener = new TcpListener(IPAddress.Parse("127.0.0.1"), puerto);
            videoListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            videoListener.Start();
            videoServerRunning = true;
            listBox1.Items.Add($"Servidor de video iniciado en puerto {puerto}...");

            while (videoServerRunning)
            {
                TcpClient client = videoListener.AcceptTcpClient();
                listBox1.Items.Add("Cliente de video conectado desde Python.");

                try
                {
                    NetworkStream stream = client.GetStream();
                    byte[] lengthBuffer = new byte[4];

                    while (videoServerRunning && client.Connected)
                    {
                        int bytesRead = stream.Read(lengthBuffer, 0, 4);
                        if (bytesRead < 4) break; // lectura parcial -> esperar nuevo cliente

                        int length = BitConverter.ToInt32(lengthBuffer.Reverse().ToArray(), 0);
                        if (length <= 0) continue;

                        byte[] imageBuffer = new byte[length];
                        int totalBytes = 0;

                        while (totalBytes < length)
                        {
                            int read = stream.Read(imageBuffer, totalBytes, length - totalBytes);
                            if (read <= 0) break;
                            totalBytes += read;
                        }

                        if (totalBytes == length)
                        {
                            using (var ms = new MemoryStream(imageBuffer))
                            {
                                try
                                {
                                    var bmp = new Bitmap(ms);
                                    pictureBoxPC.Invoke(new Action(() =>
                                    {
                                        pictureBoxPC.Image?.Dispose();
                                        pictureBoxPC.Image = new Bitmap(bmp);
                                    }));
                                }
                                catch
                                {
                                    listBox1.Items.Add("⚠️ Frame recibido inválido.");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    listBox1.Items.Add($"⚠️ Error en la conexión de video: {ex.Message}");
                }
                finally
                {
                    client.Close();
                    listBox1.Items.Add("Cliente de video desconectado. Esperando nueva conexión...");
                }
            }
        }
        catch (Exception ex)
        {
            listBox1.Items.Add($"❌ Error en servidor de video: {ex.Message}");
        }
    });
}



        // ==========================
        //     ACCIONES POR GESTO
        // ==========================
        private void EjecutarAccionPorGesto(string gesto)
        {
            switch (gesto.ToLower())
            {
                case "palm":
                    miDron.Despegar(20, bloquear: false, f: EnAire, param: "Volando");
                    break;

                case "puño":
                    miDron.Aterrizar(bloquear: false);
                    break;

                case "uno":
                    miDron.Mover("Forward", 10, bloquear: false);
                    break;

                case "dos":
                    miDron.CambiarHeading(90, bloquear: false);
                    break;

                case "tres":
                    miDron.CambiarHeading(270, bloquear: false);
                    break;

                default:
                    listBox1.Items.Add($"Gesto no reconocido: {gesto}");
                    break;
            }
        }

        // ==========================
        //     FORM CLOSING
        // ==========================
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            running = false;
            serverRunning = false;
            capPC?.Release();
            capDron?.Release();
            listener?.Stop();

            if (pythonProcess != null && !pythonProcess.HasExited)
                pythonProcess.Kill();  // Cierra el script al salir

            base.OnFormClosing(e);
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void btnGestos_Click(object sender, EventArgs e)
        {
            listBox1.Items.Add("Activando reconocimiento de gestos...");

            // Evita iniciar dos veces el script o los servidores
            if (pythonProcess != null && !pythonProcess.HasExited)
            {
                listBox1.Items.Add("⚠️ El script Python ya está en ejecución.");
                return;
            }

            if (serverRunning || videoServerRunning)
            {
                listBox1.Items.Add("⚠️ Los servidores ya están activos.");
                return;
            }

            if (!sistemaActivo)
            {
                IniciarServidorTCP();
                IniciarServidorVideo();
                IniciarScriptPython();
                sistemaActivo = true;
                listBox1.Items.Add("Reconocimiento de gestos activado.");
            }
            else
            {
                listBox1.Items.Add("El sistema ya está activo.");
            }
        }

        //-----Boton preparado, pero no funcionando----
        private void btnObjetos_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Módulo de reconocimiento de objetos próximamente.",
                            "En desarrollo",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
        }

    }
}
