namespace WindowsFormsApp1
{
    partial class Form1
    {
        /// <summary>
        /// Variable del diseñador necesaria.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpiar los recursos que se estén usando.
        /// </summary>
        /// <param name="disposing">true si los recursos administrados se deben desechar; false en caso contrario.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Código generado por el Diseñador de Windows Forms

        /// <summary>
        /// Método necesario para admitir el Diseñador. No se puede modificar
        /// el contenido de este método con el editor de código.
        /// </summary>
        private void InitializeComponent()
        {
            this.altLbl = new System.Windows.Forms.Label();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.pictureBoxPC = new System.Windows.Forms.PictureBox();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.btnGestos = new System.Windows.Forms.Button();
            this.btnObjetos = new System.Windows.Forms.Button();
            this.btnDetener = new System.Windows.Forms.Button();
            this.btnCopiarLog = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxPC)).BeginInit();
            this.SuspendLayout();
            // 
            // altLbl
            // 
            this.altLbl.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.altLbl.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
            this.altLbl.ForeColor = System.Drawing.Color.Red;
            this.altLbl.Location = new System.Drawing.Point(22, 309);
            this.altLbl.Name = "altLbl";
            this.altLbl.Size = new System.Drawing.Size(178, 45);
            this.altLbl.TabIndex = 1;
            this.altLbl.Text = "0";
            this.altLbl.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // button1
            // 
            this.button1.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
            this.button1.Location = new System.Drawing.Point(22, 38);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(178, 50);
            this.button1.TabIndex = 2;
            this.button1.Text = "Conectar";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click_1);
            // 
            // button2
            // 
            this.button2.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
            this.button2.Location = new System.Drawing.Point(22, 94);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(178, 50);
            this.button2.TabIndex = 3;
            this.button2.Text = "Despegar";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // button3
            // 
            this.button3.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
            this.button3.Location = new System.Drawing.Point(22, 150);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(178, 50);
            this.button3.TabIndex = 4;
            this.button3.Text = "Aterrizar";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // pictureBoxPC
            // 
            this.pictureBoxPC.Location = new System.Drawing.Point(250, 122);
            this.pictureBoxPC.Name = "pictureBoxPC";
            this.pictureBoxPC.Size = new System.Drawing.Size(525, 297);
            this.pictureBoxPC.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBoxPC.TabIndex = 5;
            this.pictureBoxPC.TabStop = false;
            // 
            // listBox1
            // 
            this.listBox1.Font = new System.Drawing.Font("Consolas", 10F);
            this.listBox1.FormattingEnabled = true;
            this.listBox1.HorizontalScrollbar = true;
            this.listBox1.ItemHeight = 23;
            this.listBox1.Location = new System.Drawing.Point(807, 94);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(685, 671);
            this.listBox1.TabIndex = 11;
            // 
            // btnGestos
            // 
            this.btnGestos.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.btnGestos.Location = new System.Drawing.Point(282, 12);
            this.btnGestos.Name = "btnGestos";
            this.btnGestos.Size = new System.Drawing.Size(219, 79);
            this.btnGestos.TabIndex = 12;
            this.btnGestos.Text = "Reconocimiento de gestos";
            this.btnGestos.UseVisualStyleBackColor = true;
            this.btnGestos.Click += new System.EventHandler(this.btnGestos_Click);
            // 
            // btnObjetos
            // 
            this.btnObjetos.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.btnObjetos.Location = new System.Drawing.Point(507, 12);
            this.btnObjetos.Name = "btnObjetos";
            this.btnObjetos.Size = new System.Drawing.Size(223, 79);
            this.btnObjetos.TabIndex = 13;
            this.btnObjetos.Text = "Reconocimiento de objetos";
            this.btnObjetos.UseVisualStyleBackColor = true;
            this.btnObjetos.Click += new System.EventHandler(this.btnObjetos_Click);
            // 
            // btnDetener
            // 
            this.btnDetener.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.btnDetener.Location = new System.Drawing.Point(736, 12);
            this.btnDetener.Name = "btnDetener";
            this.btnDetener.Size = new System.Drawing.Size(223, 79);
            this.btnDetener.TabIndex = 14;
            this.btnDetener.Text = "Detener scripts";
            this.btnDetener.UseVisualStyleBackColor = true;
            this.btnDetener.Click += new System.EventHandler(this.btnDetener_Click);
            // 
            // btnCopiarLog
            // 
            this.btnCopiarLog.Location = new System.Drawing.Point(1372, 58);
            this.btnCopiarLog.Name = "btnCopiarLog";
            this.btnCopiarLog.Size = new System.Drawing.Size(120, 30);
            this.btnCopiarLog.TabIndex = 0;
            this.btnCopiarLog.Text = "Copiar log";
            this.btnCopiarLog.UseVisualStyleBackColor = true;
            this.btnCopiarLog.Click += new System.EventHandler(this.btnCopiarLog_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1662, 1057);
            this.Controls.Add(this.btnCopiarLog);
            this.Controls.Add(this.btnDetener);
            this.Controls.Add(this.btnObjetos);
            this.Controls.Add(this.btnGestos);
            this.Controls.Add(this.listBox1);
            this.Controls.Add(this.pictureBoxPC);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.altLbl);
            this.Name = "Form1";
            this.Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxPC)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label altLbl;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.PictureBox pictureBoxPC;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.Button btnGestos;
        private System.Windows.Forms.Button btnObjetos;
        private System.Windows.Forms.Button btnDetener;
        private System.Windows.Forms.Button btnCopiarLog;

    }
}