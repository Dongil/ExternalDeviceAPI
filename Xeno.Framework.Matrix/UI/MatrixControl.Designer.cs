namespace Xeno.Framework.Matrix.UI
{
    partial class MatrixControl
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
			this._topBar = new System.Windows.Forms.Panel();
			this._lblStatus = new System.Windows.Forms.Label();
			this._ledPanel = new System.Windows.Forms.Panel();
			this._lblHeader = new System.Windows.Forms.Label();
			this._split = new System.Windows.Forms.SplitContainer();
			this._gridHost = new System.Windows.Forms.Panel();
			this._grid = new System.Windows.Forms.TableLayoutPanel();
			this._logContainer = new System.Windows.Forms.Panel();
			this._txtLog = new System.Windows.Forms.TextBox();
			this._logToolbar = new System.Windows.Forms.Panel();
			this._btnClearLog = new System.Windows.Forms.Button();
			this._batchBar = new System.Windows.Forms.Panel();
			this._lblBatchInput = new System.Windows.Forms.Label();
			this._cboInput = new System.Windows.Forms.ComboBox();
			this._btnApplyAll = new System.Windows.Forms.Button();
			this._btnPtp = new System.Windows.Forms.Button();
			this._btnRefresh = new System.Windows.Forms.Button();
			this._topBar.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this._split)).BeginInit();
			this._split.Panel1.SuspendLayout();
			this._split.Panel2.SuspendLayout();
			this._split.SuspendLayout();
			this._gridHost.SuspendLayout();
			this._logContainer.SuspendLayout();
			this._logToolbar.SuspendLayout();
			this._batchBar.SuspendLayout();
			this.SuspendLayout();
			// 
			// _topBar
			// 
			this._topBar.Controls.Add(this._lblStatus);
			this._topBar.Controls.Add(this._ledPanel);
			this._topBar.Controls.Add(this._lblHeader);
			this._topBar.Dock = System.Windows.Forms.DockStyle.Top;
			this._topBar.Location = new System.Drawing.Point(0, 0);
			this._topBar.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._topBar.Name = "_topBar";
			this._topBar.Size = new System.Drawing.Size(820, 26);
			this._topBar.TabIndex = 0;
			// 
			// _lblStatus
			// 
			this._lblStatus.AutoSize = true;
			this._lblStatus.Location = new System.Drawing.Point(220, 7);
			this._lblStatus.Name = "_lblStatus";
			this._lblStatus.Size = new System.Drawing.Size(29, 12);
			this._lblStatus.TabIndex = 2;
			this._lblStatus.Text = "대기";
			// 
			// _ledPanel
			// 
			this._ledPanel.BackColor = System.Drawing.Color.Gray;
			this._ledPanel.Location = new System.Drawing.Point(200, 7);
			this._ledPanel.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._ledPanel.Name = "_ledPanel";
			this._ledPanel.Size = new System.Drawing.Size(14, 11);
			this._ledPanel.TabIndex = 1;
			// 
			// _lblHeader
			// 
			this._lblHeader.AutoSize = true;
			this._lblHeader.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
			this._lblHeader.Location = new System.Drawing.Point(8, 6);
			this._lblHeader.Name = "_lblHeader";
			this._lblHeader.Size = new System.Drawing.Size(121, 19);
			this._lblHeader.TabIndex = 0;
			this._lblHeader.Text = "Matrix Switching";
			// 
			// _split
			// 
			this._split.Dock = System.Windows.Forms.DockStyle.Fill;
			this._split.Location = new System.Drawing.Point(0, 26);
			this._split.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._split.Name = "_split";
			this._split.Orientation = System.Windows.Forms.Orientation.Horizontal;
			// 
			// _split.Panel1
			// 
			this._split.Panel1.Controls.Add(this._gridHost);
			this._split.Panel1MinSize = 200;
			// 
			// _split.Panel2
			// 
			this._split.Panel2.Controls.Add(this._logContainer);
			this._split.Panel2MinSize = 80;
			this._split.Size = new System.Drawing.Size(820, 400);
			this._split.SplitterDistance = 272;
			this._split.SplitterWidth = 3;
			this._split.TabIndex = 1;
			// 
			// _gridHost
			// 
			this._gridHost.AutoScroll = true;
			this._gridHost.Controls.Add(this._grid);
			this._gridHost.Dock = System.Windows.Forms.DockStyle.Fill;
			this._gridHost.Location = new System.Drawing.Point(0, 0);
			this._gridHost.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._gridHost.Name = "_gridHost";
			this._gridHost.Size = new System.Drawing.Size(820, 272);
			this._gridHost.TabIndex = 0;
			// 
			// _grid
			// 
			this._grid.AutoSize = true;
			this._grid.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
			this._grid.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Single;
			this._grid.ColumnCount = 9;
			this._grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.Location = new System.Drawing.Point(0, 0);
			this._grid.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._grid.Name = "_grid";
			this._grid.RowCount = 9;
			this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this._grid.Size = new System.Drawing.Size(190, 190);
			this._grid.TabIndex = 0;
			// 
			// _logContainer
			// 
			this._logContainer.Controls.Add(this._txtLog);
			this._logContainer.Controls.Add(this._logToolbar);
			this._logContainer.Dock = System.Windows.Forms.DockStyle.Fill;
			this._logContainer.Location = new System.Drawing.Point(0, 0);
			this._logContainer.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._logContainer.Name = "_logContainer";
			this._logContainer.Size = new System.Drawing.Size(820, 125);
			this._logContainer.TabIndex = 0;
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
			this._txtLog.Size = new System.Drawing.Size(820, 99);
			this._txtLog.TabIndex = 0;
			this._txtLog.WordWrap = false;
			// 
			// _logToolbar
			// 
			this._logToolbar.Controls.Add(this._btnClearLog);
			this._logToolbar.Dock = System.Windows.Forms.DockStyle.Bottom;
			this._logToolbar.Location = new System.Drawing.Point(0, 99);
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
			// _batchBar
			// 
			this._batchBar.Controls.Add(this._lblBatchInput);
			this._batchBar.Controls.Add(this._cboInput);
			this._batchBar.Controls.Add(this._btnApplyAll);
			this._batchBar.Controls.Add(this._btnPtp);
			this._batchBar.Controls.Add(this._btnRefresh);
			this._batchBar.Dock = System.Windows.Forms.DockStyle.Bottom;
			this._batchBar.Location = new System.Drawing.Point(0, 426);
			this._batchBar.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._batchBar.Name = "_batchBar";
			this._batchBar.Padding = new System.Windows.Forms.Padding(5, 4, 5, 4);
			this._batchBar.Size = new System.Drawing.Size(820, 38);
			this._batchBar.TabIndex = 2;
			// 
			// _lblBatchInput
			// 
			this._lblBatchInput.AutoSize = true;
			this._lblBatchInput.Location = new System.Drawing.Point(8, 14);
			this._lblBatchInput.Name = "_lblBatchInput";
			this._lblBatchInput.Size = new System.Drawing.Size(61, 12);
			this._lblBatchInput.TabIndex = 0;
			this._lblBatchInput.Text = "일괄 입력:";
			// 
			// _cboInput
			// 
			this._cboInput.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this._cboInput.FormattingEnabled = true;
			this._cboInput.Location = new System.Drawing.Point(76, 11);
			this._cboInput.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._cboInput.Name = "_cboInput";
			this._cboInput.Size = new System.Drawing.Size(60, 20);
			this._cboInput.TabIndex = 1;
			// 
			// _btnApplyAll
			// 
			this._btnApplyAll.Location = new System.Drawing.Point(146, 9);
			this._btnApplyAll.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._btnApplyAll.Name = "_btnApplyAll";
			this._btnApplyAll.Size = new System.Drawing.Size(170, 22);
			this._btnApplyAll.TabIndex = 2;
			this._btnApplyAll.Text = "모든 출력에 적용 (All)";
			this._btnApplyAll.UseVisualStyleBackColor = true;
			// 
			// _btnPtp
			// 
			this._btnPtp.Location = new System.Drawing.Point(326, 9);
			this._btnPtp.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._btnPtp.Name = "_btnPtp";
			this._btnPtp.Size = new System.Drawing.Size(150, 22);
			this._btnPtp.TabIndex = 3;
			this._btnPtp.Text = "PTP 복구";
			this._btnPtp.UseVisualStyleBackColor = true;
			// 
			// _btnRefresh
			// 
			this._btnRefresh.Location = new System.Drawing.Point(486, 9);
			this._btnRefresh.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this._btnRefresh.Name = "_btnRefresh";
			this._btnRefresh.Size = new System.Drawing.Size(160, 22);
			this._btnRefresh.TabIndex = 4;
			this._btnRefresh.Text = "현재 상태 새로고침";
			this._btnRefresh.UseVisualStyleBackColor = true;
			// 
			// MatrixControl
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this._split);
			this.Controls.Add(this._batchBar);
			this.Controls.Add(this._topBar);
			this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.Name = "MatrixControl";
			this.Size = new System.Drawing.Size(820, 464);
			this._topBar.ResumeLayout(false);
			this._topBar.PerformLayout();
			this._split.Panel1.ResumeLayout(false);
			this._split.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this._split)).EndInit();
			this._split.ResumeLayout(false);
			this._gridHost.ResumeLayout(false);
			this._gridHost.PerformLayout();
			this._logContainer.ResumeLayout(false);
			this._logContainer.PerformLayout();
			this._logToolbar.ResumeLayout(false);
			this._batchBar.ResumeLayout(false);
			this._batchBar.PerformLayout();
			this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel _topBar;
        private System.Windows.Forms.Label _lblHeader;
        private System.Windows.Forms.Panel _ledPanel;
        private System.Windows.Forms.Label _lblStatus;
        private System.Windows.Forms.SplitContainer _split;
        private System.Windows.Forms.Panel _gridHost;
        private System.Windows.Forms.TableLayoutPanel _grid;
        private System.Windows.Forms.Panel _logContainer;
        private System.Windows.Forms.TextBox _txtLog;
        private System.Windows.Forms.Panel _logToolbar;
        private System.Windows.Forms.Button _btnClearLog;
        private System.Windows.Forms.Panel _batchBar;
        private System.Windows.Forms.Label _lblBatchInput;
        private System.Windows.Forms.ComboBox _cboInput;
        private System.Windows.Forms.Button _btnApplyAll;
        private System.Windows.Forms.Button _btnPtp;
        private System.Windows.Forms.Button _btnRefresh;
    }
}
