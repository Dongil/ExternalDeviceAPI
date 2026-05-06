namespace CameraController.UI
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

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this._menu = new System.Windows.Forms.MenuStrip();
            this._mnuView = new System.Windows.Forms.ToolStripMenuItem();
            this._mnuSlots3 = new System.Windows.Forms.ToolStripMenuItem();
            this._mnuSlots4 = new System.Windows.Forms.ToolStripMenuItem();
            this._mnuSlots5 = new System.Windows.Forms.ToolStripMenuItem();
            this._mnuSlots6 = new System.Windows.Forms.ToolStripMenuItem();
            this._mnuClearLog = new System.Windows.Forms.ToolStripMenuItem();

            this._cameraControl = new Xeno.Framework.Camera.UI.CameraControl();
            this._txtLog = new System.Windows.Forms.TextBox();
            this._lblLog = new System.Windows.Forms.Label();
            this._btnLogCopy = new System.Windows.Forms.Button();
            this._btnLogClear = new System.Windows.Forms.Button();
            this._btnLogSave = new System.Windows.Forms.Button();
            this._btnLogPause = new System.Windows.Forms.Button();
            this._dlgSaveLog = new System.Windows.Forms.SaveFileDialog();

            this._menu.SuspendLayout();
            this.SuspendLayout();

            //
            // Menu
            //
            this._menu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this._mnuView,
                this._mnuClearLog
            });
            this._menu.Location = new System.Drawing.Point(0, 0);
            this._menu.Name = "_menu";
            this._menu.Size = new System.Drawing.Size(900, 24);
            this._menu.TabIndex = 0;

            this._mnuView.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this._mnuSlots3,
                this._mnuSlots4,
                this._mnuSlots5,
                this._mnuSlots6
            });
            this._mnuView.Name = "_mnuView";
            this._mnuView.Size = new System.Drawing.Size(75, 20);
            this._mnuView.Text = "카메라 수";

            this._mnuSlots3.Name = "_mnuSlots3";
            this._mnuSlots3.Text = "3 대";
            this._mnuSlots4.Name = "_mnuSlots4";
            this._mnuSlots4.Text = "4 대";
            this._mnuSlots5.Name = "_mnuSlots5";
            this._mnuSlots5.Text = "5 대";
            this._mnuSlots6.Name = "_mnuSlots6";
            this._mnuSlots6.Text = "6 대";

            this._mnuClearLog.Name = "_mnuClearLog";
            this._mnuClearLog.Size = new System.Drawing.Size(75, 20);
            this._mnuClearLog.Text = "로그 지우기";

            //
            // _cameraControl
            //
            this._cameraControl.Location = new System.Drawing.Point(8, 28);
            this._cameraControl.Name = "_cameraControl";
            this._cameraControl.SlotCount = 3;
            this._cameraControl.Size = new System.Drawing.Size(880, 400);
            this._cameraControl.TabIndex = 1;

            //
            // _lblLog
            //
            this._lblLog.AutoSize = true;
            this._lblLog.Location = new System.Drawing.Point(8, 434);
            this._lblLog.Name = "_lblLog";
            this._lblLog.Size = new System.Drawing.Size(31, 12);
            this._lblLog.Text = "Log";
            this._lblLog.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);

            //
            // log toolbar buttons
            //
            this._btnLogCopy.Location = new System.Drawing.Point(615, 430);
            this._btnLogCopy.Name = "_btnLogCopy";
            this._btnLogCopy.Size = new System.Drawing.Size(60, 22);
            this._btnLogCopy.Text = "복사";
            this._btnLogCopy.UseVisualStyleBackColor = true;

            this._btnLogClear.Location = new System.Drawing.Point(680, 430);
            this._btnLogClear.Name = "_btnLogClear";
            this._btnLogClear.Size = new System.Drawing.Size(60, 22);
            this._btnLogClear.Text = "지우기";
            this._btnLogClear.UseVisualStyleBackColor = true;

            this._btnLogSave.Location = new System.Drawing.Point(745, 430);
            this._btnLogSave.Name = "_btnLogSave";
            this._btnLogSave.Size = new System.Drawing.Size(60, 22);
            this._btnLogSave.Text = "저장";
            this._btnLogSave.UseVisualStyleBackColor = true;

            this._btnLogPause.Location = new System.Drawing.Point(810, 430);
            this._btnLogPause.Name = "_btnLogPause";
            this._btnLogPause.Size = new System.Drawing.Size(78, 22);
            this._btnLogPause.Text = "일시정지";
            this._btnLogPause.UseVisualStyleBackColor = true;

            //
            // _dlgSaveLog
            //
            this._dlgSaveLog.Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*";
            this._dlgSaveLog.DefaultExt = "log";
            this._dlgSaveLog.Title = "로그 저장";

            //
            // _txtLog
            //
            this._txtLog.Location = new System.Drawing.Point(8, 455);
            this._txtLog.Multiline = true;
            this._txtLog.Name = "_txtLog";
            this._txtLog.ReadOnly = true;
            this._txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._txtLog.Size = new System.Drawing.Size(880, 110);
            this._txtLog.Font = new System.Drawing.Font("Consolas", 9F);
            this._txtLog.TabIndex = 2;
            this._txtLog.WordWrap = false;

            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 580);
            this.Controls.Add(this._cameraControl);
            this.Controls.Add(this._lblLog);
            this.Controls.Add(this._btnLogCopy);
            this.Controls.Add(this._btnLogClear);
            this.Controls.Add(this._btnLogSave);
            this.Controls.Add(this._btnLogPause);
            this.Controls.Add(this._txtLog);
            this.Controls.Add(this._menu);
            this.MainMenuStrip = this._menu;
            this.Name = "MainForm";
            this.Text = "Camera Controller (Sony VISCA)";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this._menu.ResumeLayout(false);
            this._menu.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.MenuStrip _menu;
        private System.Windows.Forms.ToolStripMenuItem _mnuView;
        private System.Windows.Forms.ToolStripMenuItem _mnuSlots3;
        private System.Windows.Forms.ToolStripMenuItem _mnuSlots4;
        private System.Windows.Forms.ToolStripMenuItem _mnuSlots5;
        private System.Windows.Forms.ToolStripMenuItem _mnuSlots6;
        private System.Windows.Forms.ToolStripMenuItem _mnuClearLog;

        private Xeno.Framework.Camera.UI.CameraControl _cameraControl;
        private System.Windows.Forms.Label _lblLog;
        private System.Windows.Forms.TextBox _txtLog;
        private System.Windows.Forms.Button _btnLogCopy;
        private System.Windows.Forms.Button _btnLogClear;
        private System.Windows.Forms.Button _btnLogSave;
        private System.Windows.Forms.Button _btnLogPause;
        private System.Windows.Forms.SaveFileDialog _dlgSaveLog;
    }
}
