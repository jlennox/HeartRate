namespace HeartRate
{
    partial class HeartRateForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.uxBpmNotifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.uxBpmLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // uxBpmNotifyIcon
            // 
            this.uxBpmNotifyIcon.Text = "notifyIcon1";
            this.uxBpmNotifyIcon.Visible = true;
            this.uxBpmNotifyIcon.Click += new System.EventHandler(this.uxBpmNotifyIcon_Click);
            // 
            // uxBpmLabel
            // 
            this.uxBpmLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.uxBpmLabel.Location = new System.Drawing.Point(0, 0);
            this.uxBpmLabel.Name = "uxBpmLabel";
            this.uxBpmLabel.Size = new System.Drawing.Size(309, 113);
            this.uxBpmLabel.TabIndex = 0;
            this.uxBpmLabel.Text = "Starting...";
            // 
            // HeartRateForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(308, 112);
            this.Controls.Add(this.uxBpmLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Name = "HeartRateForm";
            this.ShowInTaskbar = false;
            this.Text = "Heart rate monitor";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.HeartRateForm_FormClosing);
            this.Load += new System.EventHandler(this.HeartRateForm_Load);
            this.ResizeEnd += new System.EventHandler(this.HeartRateForm_ResizeEnd);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.NotifyIcon uxBpmNotifyIcon;
        private System.Windows.Forms.Label uxBpmLabel;
    }
}

