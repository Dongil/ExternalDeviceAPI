namespace PN8080Controller.UI
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this._grpConnection = new System.Windows.Forms.GroupBox();
            this._lblUiMode = new System.Windows.Forms.Label();
            this._uiModePanel = new System.Windows.Forms.Panel();
            this._rbGridUi = new System.Windows.Forms.RadioButton();
            this._rbAliasUi = new System.Windows.Forms.RadioButton();
            this._lblDevice = new System.Windows.Forms.Label();
            this._devicePanel = new System.Windows.Forms.Panel();
            this._rbPn8080 = new System.Windows.Forms.RadioButton();
            this._rbVideohub = new System.Windows.Forms.RadioButton();
            this._lblMaxInputs = new System.Windows.Forms.Label();
            this._numMaxInputs = new System.Windows.Forms.NumericUpDown();
            this._lblMaxOutputs = new System.Windows.Forms.Label();
            this._numMaxOutputs = new System.Windows.Forms.NumericUpDown();
            this._lblHost = new System.Windows.Forms.Label();
            this._txtHost = new System.Windows.Forms.TextBox();
            this._lblPort = new System.Windows.Forms.Label();
            this._txtPort = new System.Windows.Forms.TextBox();
            this._lblMode = new System.Windows.Forms.Label();
            this._modePanel = new System.Windows.Forms.Panel();
            this._rbPersistent = new System.Windows.Forms.RadioButton();
            this._rbPerCommand = new System.Windows.Forms.RadioButton();
            this._chkAutoReconnect = new System.Windows.Forms.CheckBox();
            this._btnConnect = new System.Windows.Forms.Button();
            this._btnDisconnect = new System.Windows.Forms.Button();
            this._contentPanel = new System.Windows.Forms.Panel();
            this._matrixControl = new Xeno.Framework.Matrix.UI.MatrixControl();
            this._aliasMatrixControl = new Xeno.Framework.Matrix.UI.AliasMatrixControl();
            this._grpConnection.SuspendLayout();
            this._uiModePanel.SuspendLayout();
            this._devicePanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._numMaxInputs)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._numMaxOutputs)).BeginInit();
            this._modePanel.SuspendLayout();
            this._contentPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // _grpConnection
            //
            this._grpConnection.Controls.Add(this._lblUiMode);
            this._grpConnection.Controls.Add(this._uiModePanel);
            this._grpConnection.Controls.Add(this._lblDevice);
            this._grpConnection.Controls.Add(this._devicePanel);
            this._grpConnection.Controls.Add(this._lblMaxInputs);
            this._grpConnection.Controls.Add(this._numMaxInputs);
            this._grpConnection.Controls.Add(this._lblMaxOutputs);
            this._grpConnection.Controls.Add(this._numMaxOutputs);
            this._grpConnection.Controls.Add(this._lblHost);
            this._grpConnection.Controls.Add(this._txtHost);
            this._grpConnection.Controls.Add(this._lblPort);
            this._grpConnection.Controls.Add(this._txtPort);
            this._grpConnection.Controls.Add(this._lblMode);
            this._grpConnection.Controls.Add(this._modePanel);
            this._grpConnection.Controls.Add(this._chkAutoReconnect);
            this._grpConnection.Controls.Add(this._btnConnect);
            this._grpConnection.Controls.Add(this._btnDisconnect);
            this._grpConnection.Dock = System.Windows.Forms.DockStyle.Top;
            this._grpConnection.Location = new System.Drawing.Point(0, 0);
            this._grpConnection.Name = "_grpConnection";
            this._grpConnection.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);
            this._grpConnection.Size = new System.Drawing.Size(900, 120);
            this._grpConnection.TabIndex = 0;
            this._grpConnection.TabStop = false;
            this._grpConnection.Text = "연결 설정";
            //
            // _lblDevice
            //
            this._lblDevice.AutoSize = true;
            this._lblDevice.Location = new System.Drawing.Point(15, 24);
            this._lblDevice.Name = "_lblDevice";
            this._lblDevice.Size = new System.Drawing.Size(60, 15);
            this._lblDevice.TabIndex = 0;
            this._lblDevice.Text = "기기 타입:";
            //
            // _devicePanel
            //
            this._devicePanel.Controls.Add(this._rbPn8080);
            this._devicePanel.Controls.Add(this._rbVideohub);
            this._devicePanel.Location = new System.Drawing.Point(80, 20);
            this._devicePanel.Name = "_devicePanel";
            this._devicePanel.Size = new System.Drawing.Size(330, 24);
            this._devicePanel.TabIndex = 1;
            //
            // _rbPn8080
            //
            this._rbPn8080.AutoSize = true;
            this._rbPn8080.Checked = true;
            this._rbPn8080.Location = new System.Drawing.Point(0, 2);
            this._rbPn8080.Name = "_rbPn8080";
            this._rbPn8080.Size = new System.Drawing.Size(150, 19);
            this._rbPn8080.TabIndex = 0;
            this._rbPn8080.TabStop = true;
            this._rbPn8080.Text = "PN-8080 (HDMI 8×8)";
            this._rbPn8080.UseVisualStyleBackColor = true;
            //
            // _rbVideohub
            //
            this._rbVideohub.AutoSize = true;
            this._rbVideohub.Location = new System.Drawing.Point(160, 2);
            this._rbVideohub.Name = "_rbVideohub";
            this._rbVideohub.Size = new System.Drawing.Size(160, 19);
            this._rbVideohub.TabIndex = 1;
            this._rbVideohub.Text = "Blackmagic Videohub";
            this._rbVideohub.UseVisualStyleBackColor = true;
            //
            // _lblMaxInputs
            //
            this._lblMaxInputs.AutoSize = true;
            this._lblMaxInputs.Location = new System.Drawing.Point(440, 24);
            this._lblMaxInputs.Name = "_lblMaxInputs";
            this._lblMaxInputs.Size = new System.Drawing.Size(60, 15);
            this._lblMaxInputs.TabIndex = 2;
            this._lblMaxInputs.Text = "최대 입력:";
            //
            // _numMaxInputs
            //
            this._numMaxInputs.Location = new System.Drawing.Point(505, 20);
            this._numMaxInputs.Maximum = new decimal(new int[] { 128, 0, 0, 0 });
            this._numMaxInputs.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this._numMaxInputs.Name = "_numMaxInputs";
            this._numMaxInputs.Size = new System.Drawing.Size(60, 23);
            this._numMaxInputs.TabIndex = 3;
            this._numMaxInputs.Value = new decimal(new int[] { 8, 0, 0, 0 });
            //
            // _lblMaxOutputs
            //
            this._lblMaxOutputs.AutoSize = true;
            this._lblMaxOutputs.Location = new System.Drawing.Point(580, 24);
            this._lblMaxOutputs.Name = "_lblMaxOutputs";
            this._lblMaxOutputs.Size = new System.Drawing.Size(60, 15);
            this._lblMaxOutputs.TabIndex = 4;
            this._lblMaxOutputs.Text = "최대 출력:";
            //
            // _numMaxOutputs
            //
            this._numMaxOutputs.Location = new System.Drawing.Point(645, 20);
            this._numMaxOutputs.Maximum = new decimal(new int[] { 128, 0, 0, 0 });
            this._numMaxOutputs.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this._numMaxOutputs.Name = "_numMaxOutputs";
            this._numMaxOutputs.Size = new System.Drawing.Size(60, 23);
            this._numMaxOutputs.TabIndex = 5;
            this._numMaxOutputs.Value = new decimal(new int[] { 8, 0, 0, 0 });
            //
            // _lblHost
            //
            this._lblHost.AutoSize = true;
            this._lblHost.Location = new System.Drawing.Point(15, 52);
            this._lblHost.Name = "_lblHost";
            this._lblHost.Size = new System.Drawing.Size(21, 15);
            this._lblHost.TabIndex = 6;
            this._lblHost.Text = "IP:";
            //
            // _txtHost
            //
            this._txtHost.Location = new System.Drawing.Point(45, 49);
            this._txtHost.Name = "_txtHost";
            this._txtHost.Size = new System.Drawing.Size(130, 23);
            this._txtHost.TabIndex = 7;
            //
            // _lblPort
            //
            this._lblPort.AutoSize = true;
            this._lblPort.Location = new System.Drawing.Point(190, 52);
            this._lblPort.Name = "_lblPort";
            this._lblPort.Size = new System.Drawing.Size(34, 15);
            this._lblPort.TabIndex = 8;
            this._lblPort.Text = "포트:";
            //
            // _txtPort
            //
            this._txtPort.Location = new System.Drawing.Point(230, 49);
            this._txtPort.Name = "_txtPort";
            this._txtPort.Size = new System.Drawing.Size(60, 23);
            this._txtPort.TabIndex = 9;
            //
            // _lblMode
            //
            this._lblMode.AutoSize = true;
            this._lblMode.Location = new System.Drawing.Point(310, 52);
            this._lblMode.Name = "_lblMode";
            this._lblMode.Size = new System.Drawing.Size(38, 15);
            this._lblMode.TabIndex = 10;
            this._lblMode.Text = "모드:";
            //
            // _modePanel
            //
            this._modePanel.Controls.Add(this._rbPersistent);
            this._modePanel.Controls.Add(this._rbPerCommand);
            this._modePanel.Location = new System.Drawing.Point(355, 48);
            this._modePanel.Name = "_modePanel";
            this._modePanel.Size = new System.Drawing.Size(280, 24);
            this._modePanel.TabIndex = 11;
            //
            // _rbPersistent
            //
            this._rbPersistent.AutoSize = true;
            this._rbPersistent.Checked = true;
            this._rbPersistent.Location = new System.Drawing.Point(0, 2);
            this._rbPersistent.Name = "_rbPersistent";
            this._rbPersistent.Size = new System.Drawing.Size(100, 19);
            this._rbPersistent.TabIndex = 0;
            this._rbPersistent.TabStop = true;
            this._rbPersistent.Text = "상주 (Always)";
            this._rbPersistent.UseVisualStyleBackColor = true;
            //
            // _rbPerCommand
            //
            this._rbPerCommand.AutoSize = true;
            this._rbPerCommand.Location = new System.Drawing.Point(115, 2);
            this._rbPerCommand.Name = "_rbPerCommand";
            this._rbPerCommand.Size = new System.Drawing.Size(140, 19);
            this._rbPerCommand.TabIndex = 1;
            this._rbPerCommand.Text = "단발 (Per-command)";
            this._rbPerCommand.UseVisualStyleBackColor = true;
            //
            // _chkAutoReconnect
            //
            this._chkAutoReconnect.AutoSize = true;
            this._chkAutoReconnect.Checked = true;
            this._chkAutoReconnect.CheckState = System.Windows.Forms.CheckState.Checked;
            this._chkAutoReconnect.Location = new System.Drawing.Point(645, 50);
            this._chkAutoReconnect.Name = "_chkAutoReconnect";
            this._chkAutoReconnect.Size = new System.Drawing.Size(92, 19);
            this._chkAutoReconnect.TabIndex = 12;
            this._chkAutoReconnect.Text = "자동 재연결";
            this._chkAutoReconnect.UseVisualStyleBackColor = true;
            //
            // _btnConnect
            //
            this._btnConnect.Location = new System.Drawing.Point(15, 85);
            this._btnConnect.Name = "_btnConnect";
            this._btnConnect.Size = new System.Drawing.Size(160, 28);
            this._btnConnect.TabIndex = 13;
            this._btnConnect.Text = "연결 및 컨트롤 로드";
            this._btnConnect.UseVisualStyleBackColor = true;
            //
            // _btnDisconnect
            //
            this._btnDisconnect.Enabled = false;
            this._btnDisconnect.Location = new System.Drawing.Point(185, 85);
            this._btnDisconnect.Name = "_btnDisconnect";
            this._btnDisconnect.Size = new System.Drawing.Size(100, 28);
            this._btnDisconnect.TabIndex = 14;
            this._btnDisconnect.Text = "끊기";
            this._btnDisconnect.UseVisualStyleBackColor = true;
            //
            // _lblUiMode
            //
            this._lblUiMode.AutoSize = true;
            this._lblUiMode.Location = new System.Drawing.Point(310, 91);
            this._lblUiMode.Name = "_lblUiMode";
            this._lblUiMode.Size = new System.Drawing.Size(50, 15);
            this._lblUiMode.TabIndex = 15;
            this._lblUiMode.Text = "UI 형태:";
            //
            // _uiModePanel
            //
            this._uiModePanel.Controls.Add(this._rbGridUi);
            this._uiModePanel.Controls.Add(this._rbAliasUi);
            this._uiModePanel.Location = new System.Drawing.Point(365, 87);
            this._uiModePanel.Name = "_uiModePanel";
            this._uiModePanel.Size = new System.Drawing.Size(210, 24);
            this._uiModePanel.TabIndex = 16;
            //
            // _rbGridUi
            //
            this._rbGridUi.AutoSize = true;
            this._rbGridUi.Checked = true;
            this._rbGridUi.Location = new System.Drawing.Point(0, 2);
            this._rbGridUi.Name = "_rbGridUi";
            this._rbGridUi.Size = new System.Drawing.Size(62, 19);
            this._rbGridUi.TabIndex = 0;
            this._rbGridUi.TabStop = true;
            this._rbGridUi.Text = "그리드";
            this._rbGridUi.UseVisualStyleBackColor = true;
            //
            // _rbAliasUi
            //
            this._rbAliasUi.AutoSize = true;
            this._rbAliasUi.Location = new System.Drawing.Point(75, 2);
            this._rbAliasUi.Name = "_rbAliasUi";
            this._rbAliasUi.Size = new System.Drawing.Size(80, 19);
            this._rbAliasUi.TabIndex = 1;
            this._rbAliasUi.Text = "별칭 패널";
            this._rbAliasUi.UseVisualStyleBackColor = true;
            //
            // _contentPanel
            //
            this._contentPanel.Controls.Add(this._aliasMatrixControl);
            this._contentPanel.Controls.Add(this._matrixControl);
            this._contentPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._contentPanel.Location = new System.Drawing.Point(0, 120);
            this._contentPanel.Name = "_contentPanel";
            this._contentPanel.Size = new System.Drawing.Size(900, 660);
            this._contentPanel.TabIndex = 1;
            //
            // _matrixControl
            //
            this._matrixControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this._matrixControl.Location = new System.Drawing.Point(0, 0);
            this._matrixControl.Name = "_matrixControl";
            this._matrixControl.Size = new System.Drawing.Size(900, 660);
            this._matrixControl.TabIndex = 0;
            //
            // _aliasMatrixControl
            //
            this._aliasMatrixControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this._aliasMatrixControl.Location = new System.Drawing.Point(0, 0);
            this._aliasMatrixControl.Name = "_aliasMatrixControl";
            this._aliasMatrixControl.Size = new System.Drawing.Size(900, 660);
            this._aliasMatrixControl.TabIndex = 1;
            this._aliasMatrixControl.Visible = false;
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 780);
            this.Controls.Add(this._contentPanel);
            this.Controls.Add(this._grpConnection);
            this.MinimumSize = new System.Drawing.Size(900, 680);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "PN-8080 / Videohub Matrix Controller (테스트 하네스)";
            this._grpConnection.ResumeLayout(false);
            this._grpConnection.PerformLayout();
            this._uiModePanel.ResumeLayout(false);
            this._uiModePanel.PerformLayout();
            this._devicePanel.ResumeLayout(false);
            this._devicePanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._numMaxInputs)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._numMaxOutputs)).EndInit();
            this._modePanel.ResumeLayout(false);
            this._modePanel.PerformLayout();
            this._contentPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.GroupBox _grpConnection;
        private System.Windows.Forms.Label _lblDevice;
        private System.Windows.Forms.Panel _devicePanel;
        private System.Windows.Forms.RadioButton _rbPn8080;
        private System.Windows.Forms.RadioButton _rbVideohub;
        private System.Windows.Forms.Label _lblMaxInputs;
        private System.Windows.Forms.NumericUpDown _numMaxInputs;
        private System.Windows.Forms.Label _lblMaxOutputs;
        private System.Windows.Forms.NumericUpDown _numMaxOutputs;
        private System.Windows.Forms.Label _lblHost;
        private System.Windows.Forms.TextBox _txtHost;
        private System.Windows.Forms.Label _lblPort;
        private System.Windows.Forms.TextBox _txtPort;
        private System.Windows.Forms.Label _lblMode;
        private System.Windows.Forms.Panel _modePanel;
        private System.Windows.Forms.RadioButton _rbPersistent;
        private System.Windows.Forms.RadioButton _rbPerCommand;
        private System.Windows.Forms.CheckBox _chkAutoReconnect;
        private System.Windows.Forms.Button _btnConnect;
        private System.Windows.Forms.Button _btnDisconnect;
        private System.Windows.Forms.Label _lblUiMode;
        private System.Windows.Forms.Panel _uiModePanel;
        private System.Windows.Forms.RadioButton _rbGridUi;
        private System.Windows.Forms.RadioButton _rbAliasUi;
        private System.Windows.Forms.Panel _contentPanel;
        private Xeno.Framework.Matrix.UI.MatrixControl _matrixControl;
        private Xeno.Framework.Matrix.UI.AliasMatrixControl _aliasMatrixControl;
    }
}
