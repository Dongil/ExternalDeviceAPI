namespace Xeno.Framework.Matrix.UI
{
    partial class AliasMatrixControl
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
			this._batchBar = new System.Windows.Forms.Panel();
			this._lblPending = new System.Windows.Forms.Label();
			this._chkAutoTake = new System.Windows.Forms.CheckBox();
			this._btnTake = new System.Windows.Forms.Button();
			this._btnRefresh = new System.Windows.Forms.Button();
			this._btnPtp = new System.Windows.Forms.Button();
			this._btnApplyAll = new System.Windows.Forms.Button();
			this._logContainer = new System.Windows.Forms.Panel();
			this._txtLog = new System.Windows.Forms.TextBox();
			this._logToolbar = new System.Windows.Forms.Panel();
			this._btnClearLog = new System.Windows.Forms.Button();
			this._gridHost = new System.Windows.Forms.Panel();
			this._grid = new System.Windows.Forms.TableLayoutPanel();
			this._batchBar.SuspendLayout();
			this._logContainer.SuspendLayout();
			this._logToolbar.SuspendLayout();
			this._gridHost.SuspendLayout();
			this.SuspendLayout();
			// 
			// _batchBar
			// 
			this._batchBar.Controls.Add(this._lblPending);
			this._batchBar.Controls.Add(this._chkAutoTake);
			this._batchBar.Controls.Add(this._btnTake);
			this._batchBar.Controls.Add(this._btnRefresh);
			this._batchBar.Controls.Add(this._btnPtp);
			this._batchBar.Controls.Add(this._btnApplyAll);
			this._batchBar.Dock = System.Windows.Forms.DockStyle.Top;
			this._batchBar.Location = new System.Drawing.Point(0, 0);
			this._batchBar.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._batchBar.Name = "_batchBar";
			this._batchBar.Padding = new System.Windows.Forms.Padding(8, 5, 8, 5);
			this._batchBar.Size = new System.Drawing.Size(820, 35);
			this._batchBar.TabIndex = 0;
			// 
			// _lblPending
			// 
			this._lblPending.AutoSize = true;
			this._lblPending.Location = new System.Drawing.Point(648, 11);
			this._lblPending.Name = "_lblPending";
			this._lblPending.Size = new System.Drawing.Size(65, 12);
			this._lblPending.TabIndex = 5;
			this._lblPending.Text = "Pending: 0";
			// 
			// _chkAutoTake
			// 
			this._chkAutoTake.AutoSize = true;
			this._chkAutoTake.Location = new System.Drawing.Point(548, 10);
			this._chkAutoTake.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._chkAutoTake.Name = "_chkAutoTake";
			this._chkAutoTake.Size = new System.Drawing.Size(81, 16);
			this._chkAutoTake.TabIndex = 4;
			this._chkAutoTake.Text = "Auto Take";
			this._chkAutoTake.UseVisualStyleBackColor = true;
			// 
			// _btnTake
			// 
			this._btnTake.Enabled = false;
			this._btnTake.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
			this._btnTake.Location = new System.Drawing.Point(450, 6);
			this._btnTake.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._btnTake.Name = "_btnTake";
			this._btnTake.Size = new System.Drawing.Size(90, 22);
			this._btnTake.TabIndex = 3;
			this._btnTake.Text = "Take";
			this._btnTake.UseVisualStyleBackColor = true;
			// 
			// _btnRefresh
			// 
			this._btnRefresh.Location = new System.Drawing.Point(286, 6);
			this._btnRefresh.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._btnRefresh.Name = "_btnRefresh";
			this._btnRefresh.Size = new System.Drawing.Size(140, 22);
			this._btnRefresh.TabIndex = 2;
			this._btnRefresh.Text = "현재 상태 새로고침";
			this._btnRefresh.UseVisualStyleBackColor = true;
			// 
			// _btnPtp
			// 
			this._btnPtp.Location = new System.Drawing.Point(178, 6);
			this._btnPtp.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._btnPtp.Name = "_btnPtp";
			this._btnPtp.Size = new System.Drawing.Size(100, 22);
			this._btnPtp.TabIndex = 1;
			this._btnPtp.Text = "PTP 복구";
			this._btnPtp.UseVisualStyleBackColor = true;
			// 
			// _btnApplyAll
			// 
			this._btnApplyAll.Location = new System.Drawing.Point(10, 6);
			this._btnApplyAll.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._btnApplyAll.Name = "_btnApplyAll";
			this._btnApplyAll.Size = new System.Drawing.Size(160, 22);
			this._btnApplyAll.TabIndex = 0;
			this._btnApplyAll.Text = "모든 출력에 적용 (All)";
			this._btnApplyAll.UseVisualStyleBackColor = true;
			// 
			// _logContainer
			// 
			this._logContainer.Controls.Add(this._txtLog);
			this._logContainer.Controls.Add(this._logToolbar);
			this._logContainer.Dock = System.Windows.Forms.DockStyle.Bottom;
			this._logContainer.Location = new System.Drawing.Point(0, 296);
			this._logContainer.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._logContainer.Name = "_logContainer";
			this._logContainer.Size = new System.Drawing.Size(820, 120);
			this._logContainer.TabIndex = 1;
			// 
			// _txtLog
			// 
			this._txtLog.BackColor = System.Drawing.Color.White;
			this._txtLog.Dock = System.Windows.Forms.DockStyle.Fill;
			this._txtLog.Font = new System.Drawing.Font("Consolas", 9F);
			this._txtLog.Location = new System.Drawing.Point(0, 0);
			this._txtLog.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._txtLog.Multiline = true;
			this._txtLog.Name = "_txtLog";
			this._txtLog.ReadOnly = true;
			this._txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this._txtLog.Size = new System.Drawing.Size(820, 94);
			this._txtLog.TabIndex = 0;
			this._txtLog.WordWrap = false;
			// 
			// _logToolbar
			// 
			this._logToolbar.Controls.Add(this._btnClearLog);
			this._logToolbar.Dock = System.Windows.Forms.DockStyle.Bottom;
			this._logToolbar.Location = new System.Drawing.Point(0, 94);
			this._logToolbar.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._logToolbar.Name = "_logToolbar";
			this._logToolbar.Size = new System.Drawing.Size(820, 26);
			this._logToolbar.TabIndex = 1;
			// 
			// _btnClearLog
			// 
			this._btnClearLog.Dock = System.Windows.Forms.DockStyle.Right;
			this._btnClearLog.Location = new System.Drawing.Point(710, 0);
			this._btnClearLog.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._btnClearLog.Name = "_btnClearLog";
			this._btnClearLog.Size = new System.Drawing.Size(110, 26);
			this._btnClearLog.TabIndex = 0;
			this._btnClearLog.Text = "로그 지우기";
			this._btnClearLog.UseVisualStyleBackColor = true;
			// 
			// _gridHost
			// 
			this._gridHost.AutoScroll = true;
			this._gridHost.Controls.Add(this._grid);
			this._gridHost.Dock = System.Windows.Forms.DockStyle.Fill;
			this._gridHost.Location = new System.Drawing.Point(0, 35);
			this._gridHost.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._gridHost.Name = "_gridHost";
			this._gridHost.Size = new System.Drawing.Size(820, 261);
			this._gridHost.TabIndex = 2;
			// 
			// _grid
			// 
			this._grid.AutoSize = true;
			this._grid.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
			this._grid.ColumnCount = 1;
			this._grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.Location = new System.Drawing.Point(0, 0);
			this._grid.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._grid.Name = "_grid";
			this._grid.RowCount = 4;
			this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.Size = new System.Drawing.Size(20, 80);
			this._grid.TabIndex = 0;
			// 
			// AliasMatrixControl
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this._gridHost);
			this.Controls.Add(this._logContainer);
			this.Controls.Add(this._batchBar);
			this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.Name = "AliasMatrixControl";
			this.Size = new System.Drawing.Size(820, 416);
			this._batchBar.ResumeLayout(false);
			this._batchBar.PerformLayout();
			this._logContainer.ResumeLayout(false);
			this._logContainer.PerformLayout();
			this._logToolbar.ResumeLayout(false);
			this._gridHost.ResumeLayout(false);
			this._gridHost.PerformLayout();
			this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel _batchBar;
        private System.Windows.Forms.Button _btnApplyAll;
        private System.Windows.Forms.Button _btnPtp;
        private System.Windows.Forms.Button _btnRefresh;
        private System.Windows.Forms.Button _btnTake;
        private System.Windows.Forms.CheckBox _chkAutoTake;
        private System.Windows.Forms.Label _lblPending;
        private System.Windows.Forms.Panel _logContainer;
        private System.Windows.Forms.TextBox _txtLog;
        private System.Windows.Forms.Panel _logToolbar;
        private System.Windows.Forms.Button _btnClearLog;
        private System.Windows.Forms.Panel _gridHost;
        private System.Windows.Forms.TableLayoutPanel _grid;
    }
}
