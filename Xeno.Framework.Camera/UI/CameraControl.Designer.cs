namespace Xeno.Framework.Camera.UI
{
    partial class CameraControl
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
            this.components = new System.ComponentModel.Container();

            this._rowsHost = new System.Windows.Forms.Panel();
            this._camButtonsHost = new System.Windows.Forms.Panel();

            this._btnConnect = new System.Windows.Forms.Button();
            this._btnDisconnect = new System.Windows.Forms.Button();

            this._btnOsdOn = new System.Windows.Forms.Button();
            this._btnOsdOff = new System.Windows.Forms.Button();
            this._btnOsdSel = new System.Windows.Forms.Button();
            this._btnOsdBack = new System.Windows.Forms.Button();

            this._lblPtz = new System.Windows.Forms.Label();
            this._btnUpLeft = new System.Windows.Forms.Button();
            this._btnUp = new System.Windows.Forms.Button();
            this._btnUpRight = new System.Windows.Forms.Button();
            this._btnLeft = new System.Windows.Forms.Button();
            this._btnRight = new System.Windows.Forms.Button();
            this._btnDownLeft = new System.Windows.Forms.Button();
            this._btnDown = new System.Windows.Forms.Button();
            this._btnDownRight = new System.Windows.Forms.Button();

            this._lblZoomFocus = new System.Windows.Forms.Label();
            this._btnZoomIn = new System.Windows.Forms.Button();
            this._btnZoomOut = new System.Windows.Forms.Button();
            this._btnFocusFar = new System.Windows.Forms.Button();
            this._btnFocusNear = new System.Windows.Forms.Button();

            this._lblSpeed = new System.Windows.Forms.Label();
            this._lblPanSpeed = new System.Windows.Forms.Label();
            this._lblTiltSpeed = new System.Windows.Forms.Label();
            this._lblZoomSpeed = new System.Windows.Forms.Label();
            this._numPanSpeed = new System.Windows.Forms.NumericUpDown();
            this._numTiltSpeed = new System.Windows.Forms.NumericUpDown();
            this._numZoomSpeed = new System.Windows.Forms.NumericUpDown();

            this._lblPreset = new System.Windows.Forms.Label();
            this._btnPresetSet = new System.Windows.Forms.Button();
            this._btnPreset1 = new System.Windows.Forms.Button();
            this._btnPreset2 = new System.Windows.Forms.Button();
            this._btnPreset3 = new System.Windows.Forms.Button();
            this._btnPreset4 = new System.Windows.Forms.Button();
            this._btnPreset5 = new System.Windows.Forms.Button();
            this._btnPreset6 = new System.Windows.Forms.Button();
            this._btnPreset7 = new System.Windows.Forms.Button();
            this._btnPreset8 = new System.Windows.Forms.Button();
            this._btnPreset9 = new System.Windows.Forms.Button();
            this._btnPreset10 = new System.Windows.Forms.Button();
            this._btnPreset11 = new System.Windows.Forms.Button();
            this._btnPreset12 = new System.Windows.Forms.Button();

            ((System.ComponentModel.ISupportInitialize)(this._numPanSpeed)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._numTiltSpeed)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._numZoomSpeed)).BeginInit();
            this.SuspendLayout();

            //
            // _rowsHost
            //
            this._rowsHost.Location = new System.Drawing.Point(8, 8);
            this._rowsHost.Name = "_rowsHost";
            this._rowsHost.Size = new System.Drawing.Size(864, 124);
            this._rowsHost.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this._rowsHost.AutoScroll = true;
            this._rowsHost.TabIndex = 0;

            //
            // _btnOsdOn / Off / Sel / Back
            //
            this._btnOsdOn.Location = new System.Drawing.Point(8, 142);
            this._btnOsdOn.Name = "_btnOsdOn";
            this._btnOsdOn.Size = new System.Drawing.Size(80, 26);
            this._btnOsdOn.Text = "OSD ON";
            this._btnOsdOn.UseVisualStyleBackColor = true;
            this._btnOsdOn.TabIndex = 1;

            this._btnOsdOff.Location = new System.Drawing.Point(94, 142);
            this._btnOsdOff.Name = "_btnOsdOff";
            this._btnOsdOff.Size = new System.Drawing.Size(80, 26);
            this._btnOsdOff.Text = "OSD OFF";
            this._btnOsdOff.UseVisualStyleBackColor = true;
            this._btnOsdOff.TabIndex = 2;

            this._btnOsdSel.Location = new System.Drawing.Point(180, 142);
            this._btnOsdSel.Name = "_btnOsdSel";
            this._btnOsdSel.Size = new System.Drawing.Size(80, 26);
            this._btnOsdSel.Text = "OSD SEL";
            this._btnOsdSel.UseVisualStyleBackColor = true;
            this._btnOsdSel.TabIndex = 3;

            this._btnOsdBack.Location = new System.Drawing.Point(266, 142);
            this._btnOsdBack.Name = "_btnOsdBack";
            this._btnOsdBack.Size = new System.Drawing.Size(80, 26);
            this._btnOsdBack.Text = "OSD BACK";
            this._btnOsdBack.UseVisualStyleBackColor = true;
            this._btnOsdBack.TabIndex = 4;

            //
            // _btnConnect / Disconnect
            //
            this._btnConnect.Location = new System.Drawing.Point(692, 142);
            this._btnConnect.Name = "_btnConnect";
            this._btnConnect.Size = new System.Drawing.Size(85, 26);
            this._btnConnect.Text = "연  결";
            this._btnConnect.UseVisualStyleBackColor = true;
            this._btnConnect.TabIndex = 5;

            this._btnDisconnect.Location = new System.Drawing.Point(783, 142);
            this._btnDisconnect.Name = "_btnDisconnect";
            this._btnDisconnect.Size = new System.Drawing.Size(85, 26);
            this._btnDisconnect.Text = "연결 종료";
            this._btnDisconnect.UseVisualStyleBackColor = true;
            this._btnDisconnect.TabIndex = 6;

            //
            // _camButtonsHost
            //
            this._camButtonsHost.Location = new System.Drawing.Point(8, 174);
            this._camButtonsHost.Name = "_camButtonsHost";
            this._camButtonsHost.Size = new System.Drawing.Size(864, 32);
            this._camButtonsHost.TabIndex = 7;

            //
            // PTZ pad area (Y=215)
            //
            this._lblPtz.Location = new System.Drawing.Point(8, 213);
            this._lblPtz.Name = "_lblPtz";
            this._lblPtz.Size = new System.Drawing.Size(120, 16);
            this._lblPtz.Text = "Pan / Tilt";
            this._lblPtz.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);

            this._btnUpLeft.Location = new System.Drawing.Point(8, 232);
            this._btnUpLeft.Name = "_btnUpLeft";
            this._btnUpLeft.Size = new System.Drawing.Size(70, 50);
            this._btnUpLeft.Text = "↖";
            this._btnUpLeft.UseVisualStyleBackColor = true;

            this._btnUp.Location = new System.Drawing.Point(82, 232);
            this._btnUp.Name = "_btnUp";
            this._btnUp.Size = new System.Drawing.Size(70, 50);
            this._btnUp.Text = "▲";
            this._btnUp.UseVisualStyleBackColor = true;

            this._btnUpRight.Location = new System.Drawing.Point(156, 232);
            this._btnUpRight.Name = "_btnUpRight";
            this._btnUpRight.Size = new System.Drawing.Size(70, 50);
            this._btnUpRight.Text = "↗";
            this._btnUpRight.UseVisualStyleBackColor = true;

            this._btnLeft.Location = new System.Drawing.Point(8, 286);
            this._btnLeft.Name = "_btnLeft";
            this._btnLeft.Size = new System.Drawing.Size(70, 50);
            this._btnLeft.Text = "◀";
            this._btnLeft.UseVisualStyleBackColor = true;

            this._btnRight.Location = new System.Drawing.Point(156, 286);
            this._btnRight.Name = "_btnRight";
            this._btnRight.Size = new System.Drawing.Size(70, 50);
            this._btnRight.Text = "▶";
            this._btnRight.UseVisualStyleBackColor = true;

            this._btnDownLeft.Location = new System.Drawing.Point(8, 340);
            this._btnDownLeft.Name = "_btnDownLeft";
            this._btnDownLeft.Size = new System.Drawing.Size(70, 50);
            this._btnDownLeft.Text = "↙";
            this._btnDownLeft.UseVisualStyleBackColor = true;

            this._btnDown.Location = new System.Drawing.Point(82, 340);
            this._btnDown.Name = "_btnDown";
            this._btnDown.Size = new System.Drawing.Size(70, 50);
            this._btnDown.Text = "▼";
            this._btnDown.UseVisualStyleBackColor = true;

            this._btnDownRight.Location = new System.Drawing.Point(156, 340);
            this._btnDownRight.Name = "_btnDownRight";
            this._btnDownRight.Size = new System.Drawing.Size(70, 50);
            this._btnDownRight.Text = "↘";
            this._btnDownRight.UseVisualStyleBackColor = true;

            //
            // Zoom / Focus area (X=240, Y=215)
            //
            this._lblZoomFocus.Location = new System.Drawing.Point(240, 213);
            this._lblZoomFocus.Name = "_lblZoomFocus";
            this._lblZoomFocus.Size = new System.Drawing.Size(160, 16);
            this._lblZoomFocus.Text = "Zoom / Focus";
            this._lblZoomFocus.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);

            this._btnZoomIn.Location = new System.Drawing.Point(240, 232);
            this._btnZoomIn.Name = "_btnZoomIn";
            this._btnZoomIn.Size = new System.Drawing.Size(78, 36);
            this._btnZoomIn.Text = "Zoom +";
            this._btnZoomIn.UseVisualStyleBackColor = true;

            this._btnZoomOut.Location = new System.Drawing.Point(322, 232);
            this._btnZoomOut.Name = "_btnZoomOut";
            this._btnZoomOut.Size = new System.Drawing.Size(78, 36);
            this._btnZoomOut.Text = "Zoom -";
            this._btnZoomOut.UseVisualStyleBackColor = true;

            this._btnFocusFar.Location = new System.Drawing.Point(240, 274);
            this._btnFocusFar.Name = "_btnFocusFar";
            this._btnFocusFar.Size = new System.Drawing.Size(78, 36);
            this._btnFocusFar.Text = "Focus +";
            this._btnFocusFar.UseVisualStyleBackColor = true;

            this._btnFocusNear.Location = new System.Drawing.Point(322, 274);
            this._btnFocusNear.Name = "_btnFocusNear";
            this._btnFocusNear.Size = new System.Drawing.Size(78, 36);
            this._btnFocusNear.Text = "Focus -";
            this._btnFocusNear.UseVisualStyleBackColor = true;

            //
            // Speed area (X=240, Y=320)
            //
            this._lblSpeed.Location = new System.Drawing.Point(240, 320);
            this._lblSpeed.Name = "_lblSpeed";
            this._lblSpeed.Size = new System.Drawing.Size(160, 16);
            this._lblSpeed.Text = "속도 (선택 카메라 기준)";
            this._lblSpeed.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);

            this._lblPanSpeed.Location = new System.Drawing.Point(240, 340);
            this._lblPanSpeed.Name = "_lblPanSpeed";
            this._lblPanSpeed.Size = new System.Drawing.Size(40, 18);
            this._lblPanSpeed.Text = "Pan";
            this._lblPanSpeed.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            this._numPanSpeed.Location = new System.Drawing.Point(282, 338);
            this._numPanSpeed.Name = "_numPanSpeed";
            this._numPanSpeed.Size = new System.Drawing.Size(60, 23);
            this._numPanSpeed.Minimum = 1;
            this._numPanSpeed.Maximum = 24;
            this._numPanSpeed.Value = 8;

            this._lblTiltSpeed.Location = new System.Drawing.Point(240, 364);
            this._lblTiltSpeed.Name = "_lblTiltSpeed";
            this._lblTiltSpeed.Size = new System.Drawing.Size(40, 18);
            this._lblTiltSpeed.Text = "Tilt";
            this._lblTiltSpeed.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            this._numTiltSpeed.Location = new System.Drawing.Point(282, 362);
            this._numTiltSpeed.Name = "_numTiltSpeed";
            this._numTiltSpeed.Size = new System.Drawing.Size(60, 23);
            this._numTiltSpeed.Minimum = 1;
            this._numTiltSpeed.Maximum = 20;
            this._numTiltSpeed.Value = 8;

            this._lblZoomSpeed.Location = new System.Drawing.Point(240, 388);
            this._lblZoomSpeed.Name = "_lblZoomSpeed";
            this._lblZoomSpeed.Size = new System.Drawing.Size(40, 18);
            this._lblZoomSpeed.Text = "Zoom";
            this._lblZoomSpeed.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            this._numZoomSpeed.Location = new System.Drawing.Point(282, 386);
            this._numZoomSpeed.Name = "_numZoomSpeed";
            this._numZoomSpeed.Size = new System.Drawing.Size(60, 23);
            this._numZoomSpeed.Minimum = 0;
            this._numZoomSpeed.Maximum = 7;
            this._numZoomSpeed.Value = 4;

            //
            // Preset area (X=420, Y=215)
            //
            this._lblPreset.Location = new System.Drawing.Point(420, 213);
            this._lblPreset.Name = "_lblPreset";
            this._lblPreset.Size = new System.Drawing.Size(200, 16);
            this._lblPreset.Text = "Preset";
            this._lblPreset.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);

            this._btnPresetSet.Location = new System.Drawing.Point(420, 232);
            this._btnPresetSet.Name = "_btnPresetSet";
            this._btnPresetSet.Size = new System.Drawing.Size(450, 30);
            this._btnPresetSet.Text = "PRESET  SET";
            this._btnPresetSet.UseVisualStyleBackColor = true;

            // 12 preset buttons in a 4 x 3 grid (110x32 each, 5px gap)
            this._btnPreset1.Location = new System.Drawing.Point(420, 270);
            this._btnPreset1.Name = "_btnPreset1";
            this._btnPreset1.Size = new System.Drawing.Size(110, 32);
            this._btnPreset1.Text = "Preset  1";
            this._btnPreset1.UseVisualStyleBackColor = true;

            this._btnPreset2.Location = new System.Drawing.Point(535, 270);
            this._btnPreset2.Name = "_btnPreset2";
            this._btnPreset2.Size = new System.Drawing.Size(110, 32);
            this._btnPreset2.Text = "Preset  2";
            this._btnPreset2.UseVisualStyleBackColor = true;

            this._btnPreset3.Location = new System.Drawing.Point(650, 270);
            this._btnPreset3.Name = "_btnPreset3";
            this._btnPreset3.Size = new System.Drawing.Size(110, 32);
            this._btnPreset3.Text = "Preset  3";
            this._btnPreset3.UseVisualStyleBackColor = true;

            this._btnPreset4.Location = new System.Drawing.Point(765, 270);
            this._btnPreset4.Name = "_btnPreset4";
            this._btnPreset4.Size = new System.Drawing.Size(105, 32);
            this._btnPreset4.Text = "Preset  4";
            this._btnPreset4.UseVisualStyleBackColor = true;

            this._btnPreset5.Location = new System.Drawing.Point(420, 308);
            this._btnPreset5.Name = "_btnPreset5";
            this._btnPreset5.Size = new System.Drawing.Size(110, 32);
            this._btnPreset5.Text = "Preset  5";
            this._btnPreset5.UseVisualStyleBackColor = true;

            this._btnPreset6.Location = new System.Drawing.Point(535, 308);
            this._btnPreset6.Name = "_btnPreset6";
            this._btnPreset6.Size = new System.Drawing.Size(110, 32);
            this._btnPreset6.Text = "Preset  6";
            this._btnPreset6.UseVisualStyleBackColor = true;

            this._btnPreset7.Location = new System.Drawing.Point(650, 308);
            this._btnPreset7.Name = "_btnPreset7";
            this._btnPreset7.Size = new System.Drawing.Size(110, 32);
            this._btnPreset7.Text = "Preset  7";
            this._btnPreset7.UseVisualStyleBackColor = true;

            this._btnPreset8.Location = new System.Drawing.Point(765, 308);
            this._btnPreset8.Name = "_btnPreset8";
            this._btnPreset8.Size = new System.Drawing.Size(105, 32);
            this._btnPreset8.Text = "Preset  8";
            this._btnPreset8.UseVisualStyleBackColor = true;

            this._btnPreset9.Location = new System.Drawing.Point(420, 346);
            this._btnPreset9.Name = "_btnPreset9";
            this._btnPreset9.Size = new System.Drawing.Size(110, 32);
            this._btnPreset9.Text = "Preset  9";
            this._btnPreset9.UseVisualStyleBackColor = true;

            this._btnPreset10.Location = new System.Drawing.Point(535, 346);
            this._btnPreset10.Name = "_btnPreset10";
            this._btnPreset10.Size = new System.Drawing.Size(110, 32);
            this._btnPreset10.Text = "Preset 10";
            this._btnPreset10.UseVisualStyleBackColor = true;

            this._btnPreset11.Location = new System.Drawing.Point(650, 346);
            this._btnPreset11.Name = "_btnPreset11";
            this._btnPreset11.Size = new System.Drawing.Size(110, 32);
            this._btnPreset11.Text = "Preset 11";
            this._btnPreset11.UseVisualStyleBackColor = true;

            this._btnPreset12.Location = new System.Drawing.Point(765, 346);
            this._btnPreset12.Name = "_btnPreset12";
            this._btnPreset12.Size = new System.Drawing.Size(105, 32);
            this._btnPreset12.Text = "Preset 12";
            this._btnPreset12.UseVisualStyleBackColor = true;

            //
            // CameraControl
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this._rowsHost);
            this.Controls.Add(this._btnOsdOn);
            this.Controls.Add(this._btnOsdOff);
            this.Controls.Add(this._btnOsdSel);
            this.Controls.Add(this._btnOsdBack);
            this.Controls.Add(this._btnConnect);
            this.Controls.Add(this._btnDisconnect);
            this.Controls.Add(this._camButtonsHost);
            this.Controls.Add(this._lblPtz);
            this.Controls.Add(this._btnUpLeft);
            this.Controls.Add(this._btnUp);
            this.Controls.Add(this._btnUpRight);
            this.Controls.Add(this._btnLeft);
            this.Controls.Add(this._btnRight);
            this.Controls.Add(this._btnDownLeft);
            this.Controls.Add(this._btnDown);
            this.Controls.Add(this._btnDownRight);
            this.Controls.Add(this._lblZoomFocus);
            this.Controls.Add(this._btnZoomIn);
            this.Controls.Add(this._btnZoomOut);
            this.Controls.Add(this._btnFocusFar);
            this.Controls.Add(this._btnFocusNear);
            this.Controls.Add(this._lblSpeed);
            this.Controls.Add(this._lblPanSpeed);
            this.Controls.Add(this._numPanSpeed);
            this.Controls.Add(this._lblTiltSpeed);
            this.Controls.Add(this._numTiltSpeed);
            this.Controls.Add(this._lblZoomSpeed);
            this.Controls.Add(this._numZoomSpeed);
            this.Controls.Add(this._lblPreset);
            this.Controls.Add(this._btnPresetSet);
            this.Controls.Add(this._btnPreset1);
            this.Controls.Add(this._btnPreset2);
            this.Controls.Add(this._btnPreset3);
            this.Controls.Add(this._btnPreset4);
            this.Controls.Add(this._btnPreset5);
            this.Controls.Add(this._btnPreset6);
            this.Controls.Add(this._btnPreset7);
            this.Controls.Add(this._btnPreset8);
            this.Controls.Add(this._btnPreset9);
            this.Controls.Add(this._btnPreset10);
            this.Controls.Add(this._btnPreset11);
            this.Controls.Add(this._btnPreset12);
            this.Name = "CameraControl";
            this.Size = new System.Drawing.Size(880, 400);
            ((System.ComponentModel.ISupportInitialize)(this._numPanSpeed)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._numTiltSpeed)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._numZoomSpeed)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Panel _rowsHost;
        private System.Windows.Forms.Panel _camButtonsHost;

        private System.Windows.Forms.Button _btnConnect;
        private System.Windows.Forms.Button _btnDisconnect;

        private System.Windows.Forms.Button _btnOsdOn;
        private System.Windows.Forms.Button _btnOsdOff;
        private System.Windows.Forms.Button _btnOsdSel;
        private System.Windows.Forms.Button _btnOsdBack;

        private System.Windows.Forms.Label _lblPtz;
        private System.Windows.Forms.Button _btnUpLeft;
        private System.Windows.Forms.Button _btnUp;
        private System.Windows.Forms.Button _btnUpRight;
        private System.Windows.Forms.Button _btnLeft;
        private System.Windows.Forms.Button _btnRight;
        private System.Windows.Forms.Button _btnDownLeft;
        private System.Windows.Forms.Button _btnDown;
        private System.Windows.Forms.Button _btnDownRight;

        private System.Windows.Forms.Label _lblZoomFocus;
        private System.Windows.Forms.Button _btnZoomIn;
        private System.Windows.Forms.Button _btnZoomOut;
        private System.Windows.Forms.Button _btnFocusFar;
        private System.Windows.Forms.Button _btnFocusNear;

        private System.Windows.Forms.Label _lblSpeed;
        private System.Windows.Forms.Label _lblPanSpeed;
        private System.Windows.Forms.Label _lblTiltSpeed;
        private System.Windows.Forms.Label _lblZoomSpeed;
        private System.Windows.Forms.NumericUpDown _numPanSpeed;
        private System.Windows.Forms.NumericUpDown _numTiltSpeed;
        private System.Windows.Forms.NumericUpDown _numZoomSpeed;

        private System.Windows.Forms.Label _lblPreset;
        private System.Windows.Forms.Button _btnPresetSet;
        private System.Windows.Forms.Button _btnPreset1;
        private System.Windows.Forms.Button _btnPreset2;
        private System.Windows.Forms.Button _btnPreset3;
        private System.Windows.Forms.Button _btnPreset4;
        private System.Windows.Forms.Button _btnPreset5;
        private System.Windows.Forms.Button _btnPreset6;
        private System.Windows.Forms.Button _btnPreset7;
        private System.Windows.Forms.Button _btnPreset8;
        private System.Windows.Forms.Button _btnPreset9;
        private System.Windows.Forms.Button _btnPreset10;
        private System.Windows.Forms.Button _btnPreset11;
        private System.Windows.Forms.Button _btnPreset12;
    }
}
