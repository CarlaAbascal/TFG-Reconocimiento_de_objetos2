using csDronLink;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

// >>> WEBRTC (ver vídeo en local dentro del Form)
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;

// >>> MQTT
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        Dron miDron = new Dron();

        // >>> WEBRTC: viewer dentro del formulario
        private WebView2 webViewRTC;

        // >>> WEBRTC: procesos python (server + publisher)
        private Process webrtcServerProcess;
        private Process webrtcPublisherProcess;

        // >>> MQTT
        private IMqttClient mqttClient;
        private bool mqttConnected = false;

        // Estados
        private bool modoGestosActivo = false;
        private bool modoObjetosActivo = false;

        Process procesoGestos;
        Process procesoObjetos;

        // Ruta del Python del entorno virtual mp_env
        string rutaPython = @"C:\Users\CARLA\Desktop\UNIVERSITAT\TFG\AA-WebRTC_objeto_gestos\TFG-Reconocimiento_de_objetos2\mp_env\Scripts\python.exe";

        public Form1()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;

            // >>> WEBRTC: crear WebView2 en el mismo sitio que el pictureBoxPC
            webViewRTC = new WebView2();
            webViewRTC.Left = pictureBoxPC.Left;
            webViewRTC.Top = pictureBoxPC.Top;
            webViewRTC.Width = pictureBoxPC.Width;
            webViewRTC.Height = pictureBoxPC.Height;
            webViewRTC.Anchor = pictureBoxPC.Anchor;

            this.Controls.Add(webViewRTC);
            webViewRTC.BringToFront();

            this.Load += async (_, __) =>
            {
                try
                {
                    await webViewRTC.EnsureCoreWebView2Async();
                    webViewRTC.Source = new Uri("http://localhost:8080/");
                }
                catch (Exception ex)
                {
                    listBox1.Items.Add($"⚠️ Error inicializando WebView2: {ex.Message}");
                }
            };
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

        // ==========================
        //     MQTT (GESTOS + OBJETOS)
        // ==========================
        private async void IniciarMQTT()
        {
            try
            {
                var factory = new MqttFactory();
                mqttClient = factory.CreateMqttClient();

                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer("127.0.0.1", 1883)
                    .Build();

                mqttClient.UseConnectedHandler(async e =>
                {
                    mqttConnected = true;
                    listBox1.Items.Add("MQTT connectat al broker.");

                    await mqttClient.SubscribeAsync("gestos");
                    listBox1.Items.Add("Subscrita al tema 'gestos'.");

                    await mqttClient.SubscribeAsync("objetos");
                    listBox1.Items.Add("Subscrita al tema 'objetos'.");
                });

                mqttClient.UseApplicationMessageReceivedHandler(e =>
                {
                    string topic = e.ApplicationMessage.Topic;
                    string mensaje = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

                    if (topic == "gestos")
                    {
                        listBox1.Items.Add($"Gesto rebut per MQTT: {mensaje}");
                        EjecutarAccionPorGesto(mensaje);
                    }
                    else if (topic == "objetos")
                    {
                        listBox1.Items.Add($"Objeto detectado por MQTT: {mensaje}");
                        // No hay acción asociada (solo mostrar)
                    }
                });

                await mqttClient.ConnectAsync(options);
            }
            catch (Exception ex)
            {
                listBox1.Items.Add($"❌ Error connectant MQTT: {ex.Message}");
            }
        }

        // ==========================
        //     BOTÓN GESTOS (MQTT)
        // ==========================
        private void btnGestos_Click(object sender, EventArgs e)
        {
            // Detener objetos si están activos
            if (procesoObjetos != null && !procesoObjetos.HasExited)
            {
                procesoObjetos.Kill();
                procesoObjetos.Dispose();
                procesoObjetos = null;
                listBox1.Items.Add("[INFO] Script de objetos detenido.");
            }

            // Iniciar gestos
            procesoGestos = new Process();
            procesoGestos.StartInfo.FileName = rutaPython;
            procesoGestos.StartInfo.Arguments = "detectar_mano.py";
            procesoGestos.StartInfo.WorkingDirectory = Application.StartupPath;
            procesoGestos.StartInfo.UseShellExecute = false;
            procesoGestos.StartInfo.CreateNoWindow = true;
            procesoGestos.Start();
            listBox1.Items.Add("[INFO] Script de gestos iniciado.");
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
        //     BOTÓN OBJETOS (WebRTC + MQTT)
        // ==========================
        private void btnObjetos_Click(object sender, EventArgs e)
        {
            // Detener gestos si están activos
            if (procesoGestos != null && !procesoGestos.HasExited)
            {
                procesoGestos.Kill();
                procesoGestos.Dispose();
                procesoGestos = null;
                listBox1.Items.Add("[INFO] Script de gestos detenido.");
            }

            // Iniciar objetos
            procesoObjetos = new Process();
            procesoObjetos.StartInfo.FileName = rutaPython;
            procesoObjetos.StartInfo.Arguments = "detectarObjetos.py";
            procesoObjetos.StartInfo.WorkingDirectory = Application.StartupPath;
            procesoObjetos.StartInfo.UseShellExecute = false;
            procesoObjetos.StartInfo.CreateNoWindow = true;
            procesoObjetos.Start();
            listBox1.Items.Add("[INFO] Script de objetos iniciado.");
        }

        // ==========================
        //     BOTÓN DETENER TODOS 
        // ==========================
        private void btnDetener_Click(object sender, EventArgs e)
        {
            if (procesoGestos != null && !procesoGestos.HasExited)
            {
                procesoGestos.Kill();
                procesoGestos.Dispose();
                procesoGestos = null;
                listBox1.Items.Add("[INFO] Script de gestos detenido.");
            }

            if (procesoObjetos != null && !procesoObjetos.HasExited)
            {
                procesoObjetos.Kill();
                procesoObjetos.Dispose();
                procesoObjetos = null;
                listBox1.Items.Add("[INFO] Script de objetos detenido.");
            }
        }


        // ==========================
        //     START PROCESS
        // ==========================
        private Process StartProcess(string exe, string args, string tag, string workingDir)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDir
            };

            var p = new Process { StartInfo = psi };
            p.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    listBox1.Items.Add($"[{tag}] {e.Data}");
            };
            p.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    listBox1.Items.Add($"⚠️ [{tag}] {e.Data}");
            };

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            return p;
        }

        // ==========================
        //     FORM CLOSING
        // ==========================
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                if (mqttClient != null && mqttConnected)
                    mqttClient.DisconnectAsync().Wait();
            }
            catch { }

            try
            {
                if (webrtcPublisherProcess != null && !webrtcPublisherProcess.HasExited)
                    webrtcPublisherProcess.Kill();

                if (webrtcServerProcess != null && !webrtcServerProcess.HasExited)
                    webrtcServerProcess.Kill();
            }
            catch { }

            base.OnFormClosing(e);
        }
    }
}
