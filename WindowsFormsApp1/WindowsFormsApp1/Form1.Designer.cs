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
            this.label1 = new System.Windows.Forms.Label();
            this.labelEstado = new System.Windows.Forms.Label();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.btnGestos = new System.Windows.Forms.Button();
            this.btnObjetos = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxPC)).BeginInit();
            this.SuspendLayout();
            // 
            // altLbl
            // 
            this.altLbl.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.altLbl.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
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
            this.button1.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
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
            this.button2.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
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
            this.button3.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
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
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(442, 113);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(75, 20);
            this.label1.TabIndex = 8;
            this.label1.Text = "Video PC";
            // 
            // labelEstado
            // 
            this.labelEstado.AutoSize = true;
            this.labelEstado.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Bold);
            this.labelEstado.ForeColor = System.Drawing.Color.Black;
            this.labelEstado.Location = new System.Drawing.Point(801, 48);
            this.labelEstado.Name = "labelEstado";
            this.labelEstado.Size = new System.Drawing.Size(271, 32);
            this.labelEstado.TabIndex = 10;
            this.labelEstado.Text = "Esperando gesto...";
            // 
            // listBox1
            // 
            this.listBox1.Font = new System.Drawing.Font("Consolas", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBox1.FormattingEnabled = true;
            this.listBox1.HorizontalScrollbar = true;
            this.listBox1.ItemHeight = 23;
            this.listBox1.Location = new System.Drawing.Point(807, 94);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(673, 303);
            this.listBox1.TabIndex = 11;
            this.listBox1.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged);
            // 
            // btnGestos
            // 
            this.btnGestos.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
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
            this.btnObjetos.Text = "Reconocimiento de objetos ";
            this.btnObjetos.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1553, 552);
            this.Controls.Add(this.btnObjetos);
            this.Controls.Add(this.btnGestos);
            this.Controls.Add(this.listBox1);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.pictureBoxPC);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.altLbl);
            this.Controls.Add(this.labelEstado);
            this.Name = "Form1";
            this.Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxPC)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label altLbl;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.PictureBox pictureBoxPC;
        private System.Windows.Forms.Button button4;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label labelEstado;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.Button btnGestos;
        private System.Windows.Forms.Button btnObjetos;

    }
}

