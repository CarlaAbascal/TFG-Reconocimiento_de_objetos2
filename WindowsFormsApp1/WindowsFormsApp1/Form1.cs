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

        // >>> WEBRTC: procesos python (de momento no se usan, pero los dejamos)
        private Process webrtcServerProcess;
        private Process webrtcPublisherProcess;

        // >>> MQTT
        private IMqttClient mqttClient;
        private bool mqttConnected = false;

        // Estados
        private bool modoGestosActivo = false;
        private bool modoObjetosActivo = false;

        // Procesos de scripts
        private Process procesoGestos;
        private Process procesoObjetos;

        // Ruta del Python de los entornos virtuales
        string rutaPythonGestos = @"C:\Users\CARLA\Desktop\UNIVERSITAT\TFG\AA-WebRTC_objeto_gestos\TFG-Reconocimiento_de_objetos2\gestos_env310\Scripts\python.exe";
        string rutaPythonObjetos = @"C:\Users\CARLA\Desktop\UNIVERSITAT\TFG\AA-WebRTC_objeto_gestos\TFG-Reconocimiento_de_objetos2\mp_env\Scripts\python.exe";

        // Ruta Scripts detección
        string rutaScripts = @"C:\Users\CARLA\Desktop\UNIVERSITAT\TFG\AA-WebRTC_objeto_gestos\TFG-Reconocimiento_de_objetos2\WindowsFormsApp1\WindowsFormsApp1\bin\Debug";

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

            // >>> Único handler de Load
            this.Load += Form1_Load;
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            // >>> INICIAR MQTT
            IniciarMQTT();

            // >>> INICIAR WEBRTC
            try
            {
                await webViewRTC.EnsureCoreWebView2Async();
                webViewRTC.Source = new Uri("http://localhost:8080/");
            }
            catch (Exception ex)
            {
                listBox1.Items.Add($"⚠️ Error inicializando WebView2: {ex.Message}");
            }
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
            try
            {
                listBox1.Items.Add("[INFO] Intentando conectar al dron en modo simulacion...");
                miDron.Conectar("simulacion");
                listBox1.Items.Add("[OK] Conexión solicitada al dron.");

                miDron.EnviarDatosTelemetria(ProcesarTelemetria);
                listBox1.Items.Add("[OK] Telemetría solicitada.");
            }
            catch (Exception ex)
            {
                listBox1.Items.Add($"[ERROR] Conectar: {ex.Message}");
            }
        }

        private void EnAire(byte id, object param)
        {
            button2.BackColor = Color.Green;
            button2.ForeColor = Color.White;
            button2.Text = (string)param;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                miDron.Despegar(20, bloquear: false, f: EnAire, param: "Volando");
                button2.BackColor = Color.Yellow;
            }
            catch (Exception ex)
            {
                listBox1.Items.Add($"[ERROR] Despegar: {ex.Message}");
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                miDron.Aterrizar(bloquear: false);
            }
            catch (Exception ex)
            {
                listBox1.Items.Add($"[ERROR] Aterrizar: {ex.Message}");
            }
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
        //     BOTÓN GESTOS
        // ==========================
        private async void btnGestos_Click(object sender, EventArgs e)
        {
            try
            {
                // Parar objetos si están en marcha
                if (procesoObjetos != null && !procesoObjetos.HasExited)
                {
                    procesoObjetos.Kill();
                    procesoObjetos.Dispose();
                    procesoObjetos = null;
                    listBox1.Items.Add("[INFO] Script de objetos detenido.");
                }

                // Parar gestos si ya estaban en marcha
                if (procesoGestos != null && !procesoGestos.HasExited)
                {
                    procesoGestos.Kill();
                    procesoGestos.Dispose();
                    procesoGestos = null;
                    listBox1.Items.Add("[INFO] Script de gestos reiniciado.");
                }

                modoGestosActivo = true;
                modoObjetosActivo = false;

                listBox1.Items.Add("[INFO] Cargando reconocimiento de gestos...");
                listBox1.Items.Add("[INFO] Script de gestos iniciado.");

                procesoGestos = StartProcess(
                    rutaPythonGestos,
                    Path.Combine(rutaScripts, "detectar_mano_mp.py"),
                    "GESTOS",
                    rutaScripts
                );

                // Esperar un poco a que el servidor local 8090 arranque
                await Task.Delay(2000);

                webViewRTC.Source = new Uri("http://127.0.0.1:8090/");
                listBox1.Items.Add("[INFO] Mostrando vídeo de gestos en el formulario.");
            }
            catch (Exception ex)
            {
                listBox1.Items.Add($"[ERROR] btnGestos_Click: {ex.Message}");
            }
        }

        // ==========================
        //     ACCIONES POR GESTO
        // ==========================
        private void EjecutarAccionPorGesto(string gesto)
        {
            try
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
            catch (Exception ex)
            {
                listBox1.Items.Add($"[ERROR] Acción por gesto '{gesto}': {ex.Message}");
            }
        }

        // ==========================
        //     BOTÓN OBJETOS
        // ==========================
        private void btnObjetos_Click(object sender, EventArgs e)
        {
            try
            {
                // Parar gestos si están en marcha
                if (procesoGestos != null && !procesoGestos.HasExited)
                {
                    procesoGestos.Kill();
                    procesoGestos.Dispose();
                    procesoGestos = null;
                    listBox1.Items.Add("[INFO] Script de gestos detenido.");
                }

                // Parar objetos si ya estaban en marcha
                if (procesoObjetos != null && !procesoObjetos.HasExited)
                {
                    procesoObjetos.Kill();
                    procesoObjetos.Dispose();
                    procesoObjetos = null;
                    listBox1.Items.Add("[INFO] Script de objetos reiniciado.");
                }

                modoGestosActivo = false;
                modoObjetosActivo = true;

                procesoObjetos = StartProcess(
                    rutaPythonObjetos,
                    Path.Combine(rutaScripts, "detectarObjetos.py"),
                    "OBJETOS",
                    rutaScripts
                );

                listBox1.Items.Add("[INFO] Script de objetos iniciado.");
                webViewRTC.Source = new Uri("http://localhost:8080/");
            }
            catch (Exception ex)
            {
                listBox1.Items.Add($"[ERROR] btnObjetos_Click: {ex.Message}");
            }
        }

        // ==========================
        //     BOTÓN DETENER TODOS
        // ==========================
        private void btnDetener_Click(object sender, EventArgs e)
        {
            try
            {
                // Detener script de gestos
                if (procesoGestos != null && !procesoGestos.HasExited)
                {
                    procesoGestos.Kill();
                    procesoGestos.Dispose();
                    procesoGestos = null;
                    listBox1.Items.Add("[INFO] Script de gestos detenido.");
                }

                // Detener script de objetos
                if (procesoObjetos != null && !procesoObjetos.HasExited)
                {
                    procesoObjetos.Kill();
                    procesoObjetos.Dispose();
                    procesoObjetos = null;
                    listBox1.Items.Add("[INFO] Script de objetos detenido.");
                }

                modoGestosActivo = false;
                modoObjetosActivo = false;

                // Volver al viewer base
                webViewRTC.Source = new Uri("http://localhost:8080/");
            }
            catch (Exception ex)
            {
                listBox1.Items.Add($"[ERROR] btnDetener_Click: {ex.Message}");
            }
        }

        // ==========================
        //     BOTÓN COPIAR LISTBOX
        // ==========================
        private void btnCopiarLog_Click(object sender, EventArgs e)
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                foreach (var item in listBox1.Items)
                    sb.AppendLine(item.ToString());

                Clipboard.SetText(sb.ToString());
                MessageBox.Show("Log copiat al porta-retalls.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error copiando log: {ex.Message}");
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
                Arguments = $"\"{args}\"",
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
                if (procesoGestos != null && !procesoGestos.HasExited)
                    procesoGestos.Kill();

                if (procesoObjetos != null && !procesoObjetos.HasExited)
                    procesoObjetos.Kill();
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