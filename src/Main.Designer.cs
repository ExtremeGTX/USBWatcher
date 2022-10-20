namespace USBWatcher
{
    partial class Main
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Main));
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.lsvEvents = new System.Windows.Forms.ListView();
            this.columnHeader5 = new System.Windows.Forms.ColumnHeader();
            this.columnHeader6 = new System.Windows.Forms.ColumnHeader();
            this.columnHeader7 = new System.Windows.Forms.ColumnHeader();
            this.lsvDevices = new System.Windows.Forms.ListView();
            this.columnHeader1 = new System.Windows.Forms.ColumnHeader();
            this.columnHeader2 = new System.Windows.Forms.ColumnHeader();
            this.columnHeader3 = new System.Windows.Forms.ColumnHeader();
            this.columnHeader4 = new System.Windows.Forms.ColumnHeader();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.clearLogsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 24);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.lsvEvents);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.lsvDevices);
            this.splitContainer1.Size = new System.Drawing.Size(448, 434);
            this.splitContainer1.SplitterDistance = 109;
            this.splitContainer1.TabIndex = 0;
            // 
            // lsvEvents
            // 
            this.lsvEvents.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lsvEvents.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader5,
            this.columnHeader6,
            this.columnHeader7});
            this.lsvEvents.Location = new System.Drawing.Point(12, 3);
            this.lsvEvents.Name = "lsvEvents";
            this.lsvEvents.Size = new System.Drawing.Size(424, 103);
            this.lsvEvents.TabIndex = 4;
            this.lsvEvents.UseCompatibleStateImageBehavior = false;
            this.lsvEvents.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader5
            // 
            this.columnHeader5.Text = "Time";
            this.columnHeader5.Width = 100;
            // 
            // columnHeader6
            // 
            this.columnHeader6.Text = "Device Name";
            this.columnHeader6.Width = 210;
            // 
            // columnHeader7
            // 
            this.columnHeader7.Text = "Event";
            this.columnHeader7.Width = 80;
            // 
            // lsvDevices
            // 
            this.lsvDevices.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lsvDevices.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2,
            this.columnHeader3,
            this.columnHeader4});
            this.lsvDevices.FullRowSelect = true;
            this.lsvDevices.LabelEdit = true;
            this.lsvDevices.Location = new System.Drawing.Point(12, 3);
            this.lsvDevices.Name = "lsvDevices";
            this.lsvDevices.Size = new System.Drawing.Size(424, 306);
            this.lsvDevices.TabIndex = 4;
            this.lsvDevices.UseCompatibleStateImageBehavior = false;
            this.lsvDevices.View = System.Windows.Forms.View.Details;
            this.lsvDevices.AfterLabelEdit += new System.Windows.Forms.LabelEditEventHandler(this.lsvDevices_AfterLabelEdit);
            this.lsvDevices.KeyDown += new System.Windows.Forms.KeyEventHandler(this.lsvDevices_KeyDown);
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "Serial Device Name";
            this.columnHeader1.Width = 230;
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "Port";
            // 
            // columnHeader3
            // 
            this.columnHeader3.Text = "VID";
            // 
            // columnHeader4
            // 
            this.columnHeader4.Text = "PID";
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutToolStripMenuItem,
            this.clearLogsToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(448, 24);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(52, 20);
            this.aboutToolStripMenuItem.Text = "About";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.aboutToolStripMenuItem_Click);
            // 
            // clearLogsToolStripMenuItem
            // 
            this.clearLogsToolStripMenuItem.Name = "clearLogsToolStripMenuItem";
            this.clearLogsToolStripMenuItem.Size = new System.Drawing.Size(71, 20);
            this.clearLogsToolStripMenuItem.Text = "Clear logs";
            this.clearLogsToolStripMenuItem.Click += new System.EventHandler(this.clearLogsToolStripMenuItem_Click);
            // 
            // Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(448, 458);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.menuStrip1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Main";
            this.Text = "USB Watcher";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private SplitContainer splitContainer1;
        private ListView lsvEvents;
        private ColumnHeader columnHeader5;
        private ColumnHeader columnHeader6;
        private ColumnHeader columnHeader7;
        private ListView lsvDevices;
        private ColumnHeader columnHeader1;
        private ColumnHeader columnHeader2;
        private ColumnHeader columnHeader3;
        private ColumnHeader columnHeader4;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private ToolStripMenuItem clearLogsToolStripMenuItem;
    }
}