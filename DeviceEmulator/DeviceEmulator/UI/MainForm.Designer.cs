namespace DeviceEmulator.UI
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private System.Windows.Forms.Label _lblDeviceType;
        private System.Windows.Forms.Label _lblModel;
        private System.Windows.Forms.Label _lblTransport;
        private System.Windows.Forms.Label _lblCom;
        private System.Windows.Forms.Label _lblBaud;
        private System.Windows.Forms.Label _lblPort;
        private System.Windows.Forms.Label _lblLatency;
        private System.Windows.Forms.Label _lblStatus;
        private System.Windows.Forms.ComboBox _cmbDeviceType;
        private System.Windows.Forms.ComboBox _cmbModel;
        private System.Windows.Forms.ComboBox _cmbTransport;
        private System.Windows.Forms.ComboBox _cmbCom;
        private System.Windows.Forms.ComboBox _cmbBaud;
        private System.Windows.Forms.TextBox _txtPort;
        private System.Windows.Forms.CheckBox _chkSonyReply;
        private System.Windows.Forms.Button _btnListen;
        private System.Windows.Forms.Button _btnStop;
        private System.Windows.Forms.NumericUpDown _numLatency;
        private System.Windows.Forms.CheckBox _chkInjectNak;
        private System.Windows.Forms.CheckBox _chkInjectTimeout;
        private System.Windows.Forms.CheckBox _chkInjectMalformed;
        private System.Windows.Forms.CheckBox _chkOnce;
        private System.Windows.Forms.Button _btnLogCopy;
        private System.Windows.Forms.Button _btnLogClear;
        private System.Windows.Forms.Button _btnLogSave;
        private System.Windows.Forms.Button _btnLogPause;
        private System.Windows.Forms.TextBox _txtLog;
        private System.Windows.Forms.SaveFileDialog _dlgSaveLog;

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this._lblDeviceType = new System.Windows.Forms.Label();
            this._lblModel = new System.Windows.Forms.Label();
            this._lblTransport = new System.Windows.Forms.Label();
            this._lblCom = new System.Windows.Forms.Label();
            this._lblBaud = new System.Windows.Forms.Label();
            this._lblPort = new System.Windows.Forms.Label();
            this._lblLatency = new System.Windows.Forms.Label();
            this._lblStatus = new System.Windows.Forms.Label();
            this._cmbDeviceType = new System.Windows.Forms.ComboBox();
            this._cmbModel = new System.Windows.Forms.ComboBox();
            this._cmbTransport = new System.Windows.Forms.ComboBox();
            this._cmbCom = new System.Windows.Forms.ComboBox();
            this._cmbBaud = new System.Windows.Forms.ComboBox();
            this._txtPort = new System.Windows.Forms.TextBox();
            this._chkSonyReply = new System.Windows.Forms.CheckBox();
            this._btnListen = new System.Windows.Forms.Button();
            this._btnStop = new System.Windows.Forms.Button();
            this._numLatency = new System.Windows.Forms.NumericUpDown();
            this._chkInjectNak = new System.Windows.Forms.CheckBox();
            this._chkInjectTimeout = new System.Windows.Forms.CheckBox();
            this._chkInjectMalformed = new System.Windows.Forms.CheckBox();
            this._chkOnce = new System.Windows.Forms.CheckBox();
            this._btnLogCopy = new System.Windows.Forms.Button();
            this._btnLogClear = new System.Windows.Forms.Button();
            this._btnLogSave = new System.Windows.Forms.Button();
            this._btnLogPause = new System.Windows.Forms.Button();
            this._txtLog = new System.Windows.Forms.TextBox();
            this._dlgSaveLog = new System.Windows.Forms.SaveFileDialog();
            ((System.ComponentModel.ISupportInitialize)(this._numLatency)).BeginInit();
            this.SuspendLayout();

            // Row 1: Device Type
            this._lblDeviceType.Text = "Device Type:";
            this._lblDeviceType.Location = new System.Drawing.Point(12, 15);
            this._lblDeviceType.Size = new System.Drawing.Size(85, 20);
            this._cmbDeviceType.Location = new System.Drawing.Point(105, 12);
            this._cmbDeviceType.Size = new System.Drawing.Size(180, 23);
            this._cmbDeviceType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;

            // Row 2: Model
            this._lblModel.Text = "Model:";
            this._lblModel.Location = new System.Drawing.Point(12, 45);
            this._lblModel.Size = new System.Drawing.Size(85, 20);
            this._cmbModel.Location = new System.Drawing.Point(105, 42);
            this._cmbModel.Size = new System.Drawing.Size(280, 23);
            this._cmbModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;

            // Row 3: Transport
            this._lblTransport.Text = "Transport:";
            this._lblTransport.Location = new System.Drawing.Point(12, 75);
            this._lblTransport.Size = new System.Drawing.Size(85, 20);
            this._cmbTransport.Location = new System.Drawing.Point(105, 72);
            this._cmbTransport.Size = new System.Drawing.Size(120, 23);
            this._cmbTransport.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;

            // Row 4: COM + Baud
            this._lblCom.Text = "COM Port:";
            this._lblCom.Location = new System.Drawing.Point(12, 105);
            this._lblCom.Size = new System.Drawing.Size(85, 20);
            this._cmbCom.Location = new System.Drawing.Point(105, 102);
            this._cmbCom.Size = new System.Drawing.Size(100, 23);
            this._cmbCom.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDown;
            this._lblBaud.Text = "Baud:";
            this._lblBaud.Location = new System.Drawing.Point(220, 105);
            this._lblBaud.Size = new System.Drawing.Size(45, 20);
            this._cmbBaud.Location = new System.Drawing.Point(270, 102);
            this._cmbBaud.Size = new System.Drawing.Size(90, 23);
            this._cmbBaud.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;

            // Row 5: Local Port + Sony reply
            this._lblPort.Text = "Local Port:";
            this._lblPort.Location = new System.Drawing.Point(12, 135);
            this._lblPort.Size = new System.Drawing.Size(85, 20);
            this._txtPort.Location = new System.Drawing.Point(105, 132);
            this._txtPort.Size = new System.Drawing.Size(100, 23);
            this._txtPort.Text = "52381";
            this._chkSonyReply.Text = "Sony reply convention (52381)";
            this._chkSonyReply.Location = new System.Drawing.Point(220, 134);
            this._chkSonyReply.Size = new System.Drawing.Size(220, 20);
            this._chkSonyReply.Checked = true;

            // Row 6: Listen / Stop / Status
            this._btnListen.Text = "Listen";
            this._btnListen.Location = new System.Drawing.Point(12, 170);
            this._btnListen.Size = new System.Drawing.Size(110, 32);
            this._btnStop.Text = "Stop";
            this._btnStop.Location = new System.Drawing.Point(130, 170);
            this._btnStop.Size = new System.Drawing.Size(110, 32);
            this._lblStatus.Text = "Stopped";
            this._lblStatus.Location = new System.Drawing.Point(255, 178);
            this._lblStatus.Size = new System.Drawing.Size(180, 20);
            this._lblStatus.AutoSize = false;
            this._lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // Row 7: Latency
            this._lblLatency.Text = "Latency (ms):";
            this._lblLatency.Location = new System.Drawing.Point(12, 215);
            this._lblLatency.Size = new System.Drawing.Size(95, 20);
            this._numLatency.Location = new System.Drawing.Point(105, 213);
            this._numLatency.Size = new System.Drawing.Size(70, 23);
            this._numLatency.Minimum = 0;
            this._numLatency.Maximum = 1000;
            this._numLatency.Value = 5;

            // Row 8: Inject options
            this._chkInjectNak.Text = "Inject NAK";
            this._chkInjectNak.Location = new System.Drawing.Point(12, 245);
            this._chkInjectNak.Size = new System.Drawing.Size(95, 20);
            this._chkInjectTimeout.Text = "Inject Timeout";
            this._chkInjectTimeout.Location = new System.Drawing.Point(115, 245);
            this._chkInjectTimeout.Size = new System.Drawing.Size(115, 20);
            this._chkInjectMalformed.Text = "Inject Malformed";
            this._chkInjectMalformed.Location = new System.Drawing.Point(235, 245);
            this._chkInjectMalformed.Size = new System.Drawing.Size(135, 20);
            this._chkOnce.Text = "1회용";
            this._chkOnce.Location = new System.Drawing.Point(380, 245);
            this._chkOnce.Size = new System.Drawing.Size(60, 20);
            this._chkOnce.Checked = true;

            // Row 9: log toolbar
            this._btnLogCopy.Text = "복  사";
            this._btnLogCopy.Location = new System.Drawing.Point(12, 280);
            this._btnLogCopy.Size = new System.Drawing.Size(70, 26);
            this._btnLogClear.Text = "지우기";
            this._btnLogClear.Location = new System.Drawing.Point(88, 280);
            this._btnLogClear.Size = new System.Drawing.Size(70, 26);
            this._btnLogSave.Text = "저  장";
            this._btnLogSave.Location = new System.Drawing.Point(164, 280);
            this._btnLogSave.Size = new System.Drawing.Size(70, 26);
            this._btnLogPause.Text = "일시정지";
            this._btnLogPause.Location = new System.Drawing.Point(240, 280);
            this._btnLogPause.Size = new System.Drawing.Size(80, 26);

            // Row 10: log text
            this._txtLog.Location = new System.Drawing.Point(12, 315);
            this._txtLog.Size = new System.Drawing.Size(610, 240);
            this._txtLog.Multiline = true;
            this._txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._txtLog.ReadOnly = true;
            this._txtLog.Font = new System.Drawing.Font("Consolas", 9F);
            this._txtLog.Anchor = (System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top
                | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left
                | System.Windows.Forms.AnchorStyles.Right);

            this._dlgSaveLog.Filter = "Log files (*.log)|*.log|All files (*.*)|*.*";

            // Form
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(640, 575);
            this.Controls.Add(this._lblDeviceType);
            this.Controls.Add(this._cmbDeviceType);
            this.Controls.Add(this._lblModel);
            this.Controls.Add(this._cmbModel);
            this.Controls.Add(this._lblTransport);
            this.Controls.Add(this._cmbTransport);
            this.Controls.Add(this._lblCom);
            this.Controls.Add(this._cmbCom);
            this.Controls.Add(this._lblBaud);
            this.Controls.Add(this._cmbBaud);
            this.Controls.Add(this._lblPort);
            this.Controls.Add(this._txtPort);
            this.Controls.Add(this._chkSonyReply);
            this.Controls.Add(this._btnListen);
            this.Controls.Add(this._btnStop);
            this.Controls.Add(this._lblStatus);
            this.Controls.Add(this._lblLatency);
            this.Controls.Add(this._numLatency);
            this.Controls.Add(this._chkInjectNak);
            this.Controls.Add(this._chkInjectTimeout);
            this.Controls.Add(this._chkInjectMalformed);
            this.Controls.Add(this._chkOnce);
            this.Controls.Add(this._btnLogCopy);
            this.Controls.Add(this._btnLogClear);
            this.Controls.Add(this._btnLogSave);
            this.Controls.Add(this._btnLogPause);
            this.Controls.Add(this._txtLog);
            this.MinimumSize = new System.Drawing.Size(560, 460);
            this.Name = "MainForm";
            this.Text = "DeviceEmulator";
            ((System.ComponentModel.ISupportInitialize)(this._numLatency)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
