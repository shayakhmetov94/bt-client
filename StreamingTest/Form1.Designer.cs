namespace Bittorrent
{
    partial class Form1
    {
        /// <summary>
        /// Обязательная переменная конструктора.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Освободить все используемые ресурсы.
        /// </summary>
        /// <param name="disposing">истинно, если управляемый ресурс должен быть удален; иначе ложно.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Код, автоматически созданный конструктором форм Windows

        /// <summary>
        /// Требуемый метод для поддержки конструктора — не изменяйте 
        /// содержимое этого метода с помощью редактора кода.
        /// </summary>
        private void InitializeComponent()
        {
            this.msgNumList = new System.Windows.Forms.ListBox();
            this.startBtn = new System.Windows.Forms.Button();
            this.overallPrBar = new System.Windows.Forms.ProgressBar();
            this.SuspendLayout();
            // 
            // msgNumList
            // 
            this.msgNumList.FormattingEnabled = true;
            this.msgNumList.Location = new System.Drawing.Point(380, 12);
            this.msgNumList.Name = "msgNumList";
            this.msgNumList.Size = new System.Drawing.Size(119, 212);
            this.msgNumList.TabIndex = 0;
            // 
            // startBtn
            // 
            this.startBtn.Location = new System.Drawing.Point(299, 192);
            this.startBtn.Name = "startBtn";
            this.startBtn.Size = new System.Drawing.Size(75, 23);
            this.startBtn.TabIndex = 1;
            this.startBtn.Text = "Download";
            this.startBtn.UseVisualStyleBackColor = true;
            this.startBtn.Click += new System.EventHandler(this.startBtn_Click);
            // 
            // overallPrBar
            // 
            this.overallPrBar.Location = new System.Drawing.Point(12, 12);
            this.overallPrBar.Name = "overallPrBar";
            this.overallPrBar.Size = new System.Drawing.Size(362, 23);
            this.overallPrBar.TabIndex = 2;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(511, 227);
            this.Controls.Add(this.overallPrBar);
            this.Controls.Add(this.startBtn);
            this.Controls.Add(this.msgNumList);
            this.Name = "Form1";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox msgNumList;
        private System.Windows.Forms.Button startBtn;
        private System.Windows.Forms.ProgressBar overallPrBar;
    }
}

