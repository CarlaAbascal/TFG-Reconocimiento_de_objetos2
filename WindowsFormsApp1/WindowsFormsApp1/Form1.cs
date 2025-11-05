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
           // Form1_Load(this, EventArgs.Empty);
        }

        // ==========================
        //     INICIALIZACIÓN
        // ==========================
      
        private void Form1_Load(object sender, EventArgs e)
        {
            // Se inicia vacío: los servidores se lanzan solo al pulsar los botones.
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
        //-----Boton Gestos-----
        private async void btnGestos_Click(object sender, EventArgs e)
        {
            // Si el modo gestos ya está activo → detenerlo
            if (modoGestosActivo)
            {
                listBox1.Items.Add("🛑 Deteniendo reconocimiento de gestos...");
                DetenerScriptPython(pythonProcess);
                serverRunning = false;
                listener?.Stop();

                // liberar vídeo
                videoServerRunning = false;
                videoListener?.Stop();

                modoGestosActivo = false;
                btnGestos.Text = "Reconocer Gestos";
                btnGestos.BackColor = SystemColors.Control;
                return;
            }


            // Si había un script de objetos, lo detenemos primero
            if (modoObjetosActivo)
            {
                listBox1.Items.Add("⛔ Cerrando reconocimiento de objetos...");
                DetenerScriptPython(pythonObjetosProcess);
                modoObjetosActivo = false;
                btnObjetos.Text = "Reconocer Objetos";
                btnObjetos.BackColor = SystemColors.Control;
                await Task.Delay(1000); // esperar a que se libere la cámara
            }

            listBox1.Items.Add("🖐 Activando reconocimiento de gestos...");

            // Iniciar servidores (solo si no están activos)
            if (!serverRunning)
                IniciarServidorTCP();
            if (!videoServerRunning)
                IniciarServidorVideo();

            IniciarScriptPython(); // tu método para detectar_mano.py
            modoGestosActivo = true;
            btnGestos.Text = "Detener Gestos";
            btnGestos.BackColor = Color.LightGreen;
        }


        //-----------------OBJETOS---------------------

        // ==========================
        //     BOTÓN OBJETOS
        // ==========================
        private TcpListener listenerObjetos;
        private bool serverObjetosRunning = false;
        private System.Diagnostics.Process pythonObjetosProcess;

        private bool modoGestosActivo = false;
        private bool modoObjetosActivo = false;

        private async void btnObjetos_Click(object sender, EventArgs e)
        {
            // Si el modo objetos ya está activo → detenerlo
            if (modoObjetosActivo)
            {
                listBox1.Items.Add("🛑 Deteniendo reconocimiento de objetos...");
                DetenerScriptPython(pythonObjetosProcess);
                serverObjetosRunning = false;
                listenerObjetos?.Stop();
                modoObjetosActivo = false;
                btnObjetos.Text = "Reconocer Objetos";
                btnObjetos.BackColor = SystemColors.Control;
                return;
            }

            // Si había un script de gestos, lo detenemos primero
            if (modoGestosActivo)
            {
                listBox1.Items.Add("⛔ Cerrando reconocimiento de gestos...");
                DetenerScriptPython(pythonProcess);
                modoGestosActivo = false;
                btnGestos.Text = "Reconocer Gestos";
                btnGestos.BackColor = SystemColors.Control;
                await Task.Delay(1000); // esperar a que se libere la cámara
            }

            listBox1.Items.Add("🔍 Activando reconocimiento de objetos...");

            // Iniciar servidor de objetos si no está activo
            if (!serverObjetosRunning)
                IniciarServidorObjetos();

            // Esperar a que el servidor levante el puerto
            await Task.Delay(2000); // ⚠️ este delay es CLAVE

            IniciarScriptPythonObjetos(); // método que lanza detectarObjetos.py
            modoObjetosActivo = true;
            btnObjetos.Text = "Detener Objetos";
            btnObjetos.BackColor = Color.LightGreen;
        }



        // ==========================
        //     SERVIDOR TCP OBJETOS
        // ==========================
        private void IniciarServidorObjetos()
        {
            Task.Run(() =>
            {
                try
                {
                    int puerto = 5007; // distinto al de gestos (5005)
                    listenerObjetos = new TcpListener(IPAddress.Parse("127.0.0.1"), puerto);
                    listenerObjetos.Start();
                    serverObjetosRunning = true;
                    listBox1.Items.Add($"Servidor TCP (objetos) iniciado en puerto {puerto}");

                    while (serverObjetosRunning)
                    {
                        TcpClient client = listenerObjetos.AcceptTcpClient();
                        listBox1.Items.Add("Cliente de objetos conectado desde Python.");

                        NetworkStream stream = client.GetStream();
                        byte[] buffer = new byte[1024];
                        StringBuilder sb = new StringBuilder();

                        while (client.Connected)
                        {
                            int bytesRead = stream.Read(buffer, 0, buffer.Length);
                            if (bytesRead == 0) break;

                            string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            sb.Append(data);

                            // Procesar mensajes separados por salto de línea
                            while (sb.ToString().Contains("\n"))
                            {
                                string line = sb.ToString();
                                int index = line.IndexOf('\n');
                                string mensaje = line.Substring(0, index).Trim();
                                sb.Remove(0, index + 1);

                                if (!string.IsNullOrWhiteSpace(mensaje))
                                {
                                    listBox1.Items.Add($"🔍 Objeto detectado: {mensaje}");
                                }
                            }
                        }

                        client.Close();
                        listBox1.Items.Add("Cliente de objetos desconectado.");
                    }
                }
                catch (Exception ex)
                {
                    listBox1.Items.Add($"❌ Error en servidor de objetos: {ex.Message}");
                }
            });
        }

        // ==========================
        //     SCRIPT PYTHON OBJETOS
        // ==========================
        private void IniciarScriptPythonObjetos()
        {
            try
            {
                string pythonExe = @"C:\Users\CARLA\AppData\Local\Programs\Python\Python310\python.exe";
                string scriptPath = Path.Combine(Application.StartupPath, "detectarObjetos.py"); // ✅ Ruta relativa al .exe

                if (!File.Exists(scriptPath))
                {
                    listBox1.Items.Add($"❌ No se encontró el script en: {scriptPath}");
                    return;
                }

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = $"\"{scriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                pythonObjetosProcess = new System.Diagnostics.Process { StartInfo = psi };
                pythonObjetosProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        listBox1.Items.Add($"[Python Objetos]: {e.Data}");
                };
                pythonObjetosProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        listBox1.Items.Add($"⚠️ [Error Python Objetos]: {e.Data}");
                };

                pythonObjetosProcess.Start();
                pythonObjetosProcess.BeginOutputReadLine();
                pythonObjetosProcess.BeginErrorReadLine();

                listBox1.Items.Add("✅ Script de reconocimiento de objetos iniciado correctamente.");
            }
            catch (Exception ex)
            {
                listBox1.Items.Add($"❌ Error al iniciar script de objetos: {ex.Message}");
            }
        }


        //-------------------DETENER SCRIPT----------------------
        private void DetenerScriptPython(System.Diagnostics.Process proceso)
        {
            try
            {
                if (proceso != null && !proceso.HasExited)
                {
                    proceso.Kill();
                    proceso.WaitForExit(); // 🔹 Esperar a que se cierre completamente
                   // proceso.Dispose();
                    listBox1.Items.Add("✅ Script Python detenido correctamente.");
                }
            }
            catch (Exception ex)
            {
                listBox1.Items.Add($"⚠️ Error al detener script: {ex.Message}");
            }
            finally
            {
                proceso?.Dispose();
                if (proceso == pythonProcess) { pythonProcess = null; modoGestosActivo = false; }
                if (proceso == pythonObjetosProcess) { pythonObjetosProcess = null; modoObjetosActivo = false; }
            }
        
        }


    }
}
