namespace psyBrowser
{
    partial class psyBrowser
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(psyBrowser));
            textBoxURL = new TextBox();
            panelRenderer = new Panel();
            progressBarPageLoading = new ProgressBar();
            SuspendLayout();
            // 
            // textBoxURL
            // 
            textBoxURL.BackColor = Color.DimGray;
            textBoxURL.BorderStyle = BorderStyle.FixedSingle;
            textBoxURL.Cursor = Cursors.IBeam;
            textBoxURL.Dock = DockStyle.Top;
            textBoxURL.Location = new Point(0, 0);
            textBoxURL.Margin = new Padding(5);
            textBoxURL.MaxLength = 99999;
            textBoxURL.Name = "textBoxURL";
            textBoxURL.PlaceholderText = "[ URL | Search ] + Enter";
            textBoxURL.Size = new Size(2022, 39);
            textBoxURL.TabIndex = 0;
            textBoxURL.WordWrap = false;
            // 
            // panelRenderer
            // 
            panelRenderer.Dock = DockStyle.Fill;
            panelRenderer.Location = new Point(0, 39);
            panelRenderer.Name = "panelRenderer";
            panelRenderer.Size = new Size(2022, 914);
            panelRenderer.TabIndex = 1;
            // 
            // progressBarPageLoading
            // 
            progressBarPageLoading.Dock = DockStyle.Top;
            progressBarPageLoading.Location = new Point(0, 39);
            progressBarPageLoading.Margin = new Padding(0);
            progressBarPageLoading.Name = "progressBarPageLoading";
            progressBarPageLoading.Size = new Size(2022, 3);
            progressBarPageLoading.TabIndex = 0;
            // 
            // psyBrowser
            // 
            AccessibleDescription = "psy Web Browser";
            AutoScaleDimensions = new SizeF(13F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Control;
            ClientSize = new Size(2022, 953);
            Controls.Add(progressBarPageLoading);
            Controls.Add(panelRenderer);
            Controls.Add(textBoxURL);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "psyBrowser";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "psyBrowser";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox textBoxURL;
        private Panel panelRenderer;
        private ProgressBar progressBarPageLoading;
    }
}
