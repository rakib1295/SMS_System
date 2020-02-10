using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Oracle.ManagedDataAccess.Client;
using System.ComponentModel;
using System.Deployment.Application;
using System.Net.NetworkInformation;

namespace SMS_System
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //creating TNS entries
        string OracldbLive = "Data Source=(DESCRIPTION =" +
        "(ADDRESS = (PROTOCOL = TCP)(HOST = 10.10.10.9)(PORT = 1521))" +
        "(CONNECT_DATA =" +
        "(SERVER = DEDICATED)" +
        "(SERVICE_NAME = ora11g)));" +
        "User Id= amc1;Password=amc1;";

        //string oradb = "Data Source=(DESCRIPTION="
        // + "(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=1521)))"
        // + "(CONNECT_DATA=(SERVER=DEDICATED)(SERVICE_NAME=ORCL)));"
        // + "User Id=Rakib;Password=1234567;";

        int NumberofDestination = 0; // number of destination
        int Send_Status = 0;
        int SMSData_Status = 0; //status of oracle connection of Live database
        int SMSDestination_Status = 0; //status of oracle connection of SMS destination
        int NumberofFailtoGenerateSMS = 0;
        String Destination_Number_url = "";
        bool _reset = false;

        double incoming_Diff_actual = 0;
        double outgoing_Diff_actual = 0;
        double incoming_acceptance_diff = 0.3; //%
        string AdditionalITXPhnNumString = ""; 
        string reportingPhnNumString = "";
        bool _error_checkOrNot = true;
        bool _account_check = true;

        Dictionary<String, String> SMSData = new Dictionary<String, String>();
        List <String> Dest_ANS = new List<String>();
        List <String> Dest_ICX = new List<String>();
        List <String> Dest_ITX = new List<String>();

        bool _ans = true;
        bool _icx = true;
        bool _itx = true;
        bool _itx_ = true;
        bool _itx_err_send = false;

        bool Query_Only = false;
        string CreditStatus_Text = "";
        int CreditStatus_Today = 0, CreditStatus_Yesterday = 0, CreditDeducted_Yesterday = 0;

        string responseFromHttpWeb = "";
        int SubtractiveDataDay = 1;

        public MainWindow()
        {
            InitializeComponent();
            Browse_Btn_Animation();
            _run3.Text = "Instructions of using this app:";
            _run4.Text = Show_Instructions();
            this.PropertyChanged += MainWindow_PropertyChanged;
            DispatcherTimerClock();

            
            Application.Current.MainWindow.Closing += MainWindow_Closing;
            Application.Current.MainWindow.Loaded += MainWindow_Loaded;

#if !DEBUG
            versionNumber.Text = "Version: " + ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString(4);
#endif
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            report_textbox.Text = Properties.Settings.Default.ErrorReportPhnNumbers;
            error_checkbox.IsChecked = Properties.Settings.Default.CheckError;
            ITX_error_Send.IsChecked = Properties.Settings.Default.SendErrorReport;
            error_parcent_textbox.Text = Properties.Settings.Default.ErrorLimit;
            user_name.Text = Properties.Settings.Default.UserName;
            acc_psw.Password = Properties.Settings.Default.Password;
            ITXAdditional_checkbox.IsChecked = Properties.Settings.Default.SendITXAdd;
            AdditionalMsgTime_txtbox.Text = Properties.Settings.Default.SMSTimeAdd;
            AdditionalMsgPhnNumber_txtbox.Text = Properties.Settings.Default.ITXAddPhnNumbers;
            Account_check.IsChecked = Properties.Settings.Default.CheckCredit;
            Alarm_time_textbox.Text = Properties.Settings.Default.SMSTime;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (MessageBox.Show("Do you want to close the app?", Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            {
                e.Cancel = true;
            }
            else
            {
                Properties.Settings.Default.ErrorReportPhnNumbers = report_textbox.Text;
                Properties.Settings.Default.CheckError = (bool)error_checkbox.IsChecked;
                Properties.Settings.Default.SendErrorReport = (bool)ITX_error_Send.IsChecked;
                Properties.Settings.Default.ErrorLimit = error_parcent_textbox.Text;
                Properties.Settings.Default.UserName = user_name.Text;
                Properties.Settings.Default.Password = acc_psw.Password;
                Properties.Settings.Default.SendITXAdd = (bool)ITXAdditional_checkbox.IsChecked;
                Properties.Settings.Default.SMSTimeAdd = AdditionalMsgTime_txtbox.Text;
                Properties.Settings.Default.ITXAddPhnNumbers = AdditionalMsgPhnNumber_txtbox.Text;
                Properties.Settings.Default.CheckCredit = (bool)Account_check.IsChecked;
                Properties.Settings.Default.SMSTime = Alarm_time_textbox.Text;

                Properties.Settings.Default.Save();
            }
        }

        public bool Error_CheckOrNot
        {
            get { return _error_checkOrNot; }
            set
            {
                _error_checkOrNot = value;
                // Call OnPropertyChanged whenever the property is updated
                OnPropertyChanged("ITXErrorChecking");
            }
        }

        public bool Balance_CheckOrNot
        {
            get { return _account_check; }
            set
            {
                _account_check = value;
                // Call OnPropertyChanged whenever the property is updated
                OnPropertyChanged("AccountBalanceChecking");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        private void ReplyTextblock_clean()
        {
            try
            {
                _run3.Text = "";
                _run4.Text = "";
                _run5.Text = "";
                _run6.Text = "";
                _run7.Text = "";
                _run8.Text = "";
                _run11.Text = "";
                _run12.Text = "";

            }
            catch (Exception ex)
            {
                Show_LogTextblock(ex.Message);
                Write_logFile(ex.Message);
            }
        }

        private string Show_Instructions()
        {
            return
                "\n  1. Please at first browse text file for destination phone numbers if browse button blinks. Each line of text file format: PHONE-NUMBER<Space>GROUP<Space>Person-Name(optional).\n(e.g. 01XXXXXXXXX ICX Mr. Abc)." +
                "\n  2. Please make sure your PC is connected to oracle billing database." +
                "\n  3. Please make sure your PC is connected to internet." +
                "\n  4. Be careful about SMS credit balance and expiration date, you can check it by logging in teletalk bulksms portal:- http://bulksms.teletalk.com.bd/." +
                "\n  5. Each log data will be saved to this directory:- C:\\Users\\Public\\SMSApp_Log_" + DateTime.Now.Year.ToString() + ".txt." +
                "\n  6. Adjust the trigger time at the box above if needed (Default time is 9:00:00 AM)." +
                "\n  7. SMS will not be sent twice a day, if you need to send twice then click 'Reset' button." +
                "\n  8. Check or uncheck any message group, if you need to send SMS to any specific group." +
                "\n  9. If you need to send data for a specific date, at first click the 'Reset' button then enable calender. Then select a date and click 'Send SMS' button." +
                "\n  10. If you need to query the sms contents without sending sms for any specific date, select a date and click the 'Query Traffic Data' button. (By default yesterday's data will be shown.)" +
                "\n  11. To stop any process or running thread or work, reset the app by clicking 'Reset' button." +
                "\n  12. At settings wizard, set ICX vs ITX incoming traffic difference acceptance limit in percentage (%) and set phone numbers to send error report if needed. And also teletalk account settings can be set." +
                "\n  13. If Additional ITX message is needed for current (today's) date, then enable 'ITX Additional Message' and adjust the trigger time and give phone numbers.";
        }

        private void Browse_Btn_Animation()
        {
            try
            {
                SolidColorBrush Browse_Btn_Brush = new SolidColorBrush();
                ColorAnimation colorAnimation = new ColorAnimation(Colors.Red, TimeSpan.FromMilliseconds(500));


                colorAnimation.RepeatBehavior = RepeatBehavior.Forever;
                colorAnimation.AutoReverse = true;
                Browse_Btn_Brush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);
                Browse_btn.Background = Browse_Btn_Brush;
            }
            catch (Exception ex)
            {
                Show_LogTextblock(ex.Message);
                Write_logFile(ex.Message);
            }
        }



        void Show_LogTextblock(String str)
        {
            try
            {
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    log_textblock.Text = log_textblock.Text + "# " + DateTime.Now.ToLongTimeString() + ":- " + str + "\n";
                    _scrollbar_log.ScrollToBottom();
                }));

            }
            catch (Exception ex)
            {
                Write_logFile(ex.Message);
            }
        }

        private Object thisLock = new Object();

        void Write_logFile(String str)///////////////////////////////////////////////////////////////////////////////////////////////////
        {
            try
            {
                lock (thisLock)
                {
                    System.IO.StreamWriter file = new System.IO.StreamWriter(@"c:\\Users\\Public\\SMSApp_Log_" + DateTime.Now.Year.ToString() + ".txt", true);

                    file.WriteLine(DateTime.Now.ToString() + ":- " + str);
                    file.Close();
                }
            }
            catch (Exception ex)
            {
                Show_LogTextblock(ex.Message);
            }
        }


        void DispatcherTimerClock()
        {
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += timer_Tick;
            timer.Start();
        }

        void timer_Tick(object sender, EventArgs e)
        {
            string CurrentTime;
            CurrentTime = DateTime.Now.ToLongTimeString();
            clock_textblock.Text = CurrentTime; //time showing

            if (CurrentTime == "12:00:00 AM") //###################################################CONSIDER ALWAYS########################################################
            {
                _reset = true;
                SMSData_Status = 0;
                //SMSDestination_Status = 0;
                Send_Status = 0;
                NumberofDestination = 0;

                SubtractiveDataDay = 1;
                _date_picker.IsEnabled = false;
                calender_btn.Content = "Enable Calender";
                _date_picker.SelectedDate = DateTime.Today.Subtract(TimeSpan.FromDays(1));


                if (CreditStatus_Yesterday - CreditStatus_Today >= 0)
                    CreditDeducted_Yesterday = CreditStatus_Yesterday - CreditStatus_Today;

                CreditStatus_Yesterday = CreditStatus_Today;
                log_textblock.Text = "";

                Write_logFile("Successfully cleared previous day status.");
            }

            if (CurrentTime == EventTime1) //time for event fire
            {
                Send_Status = 0;
                SentMsgCount = 0;
                SMSData_Status = 0;
                //SMSDestination_Status = 0;
                SubtractiveDataDay = 1;
                Query_Only = false;

                if (_ans | _icx | _itx)
                {
                    _reset = false;
                    ThreadHandle("AUTO");
                }
                else
                {
                    Show_LogTextblock("No message group is selected. Please select at least one of them to send SMS.");
                    //MessageBox.Show("Please select at least one message group to send SMS.");
                }                
            }
            else if (CurrentTime == EventTime2 && Send_ITX_AdditionalMsg)
            {
                SentMsgCount = 0;
                SMSData_Status = 0;
                //SMSDestination_Status = 0;
                Query_Only = false;
                SubtractiveDataDay = 0;
                Send_Status = 0;

                _reset = false;
                if (AdditionalMsgPhnNumber_txtbox.Text != "")
                {
                    ThreadHandle("ADDITIONAL");
                }
                else
                {
                    Write_logFile("No phone number to send additional ITX traffic.");
                    Show_LogTextblock("No phone number to send additional ITX traffic.");
                }
            }
        }

        bool Send_ITX_AdditionalMsg = false;

        private async void ThreadHandle(String _generator)
        {
            await ThreadHandleAsync(_generator);
        }

        public Task ThreadHandleAsync(String _generator)
        {
            return Task.Run(() => FetchingSMSData(_generator));
        }


        private void FetchingSMSData(String _generator)
        {
            bool send_error_msg = false;
            if (SMSData_Status == 0)
            {
                Show_LogTextblock("Fetching SMS contents from Oracle Live database, please wait..... .... ... .. .");
                try
                {
                    SMSData.Clear();
                    OracleConnection conn = new OracleConnection(OracldbLive);  // C#
                    conn.Open();
                    OracleCommand cmd = new OracleCommand();
                    cmd.CommandType = CommandType.Text;
                    cmd.Connection = conn;


                    if (_ans)
                    {
                        cmd.CommandText = "SELECT (IDDALL.IDD || DOMALL.DOM) SMSCONTENT FROM " +
                            "(SELECT 1 sl, (IDDINC.IDDIncomming || IDDOUTG.IDDOutgoing) IDD FROM " +
                            "(select 1 sl, ('ANS traffic for ' || TO_CHAR((sysdate-" + SubtractiveDataDay + "),'dd/mm') || ' Intl In: ' || to_char(round(sum(t.duration_float),0),'999,999,999,999')|| ' pm') IDDIncomming from cdr_inter_ans_stat t " +
                            "where  t.transit_type = '32' AND t.billingcycle = TO_CHAR((sysdate-" + SubtractiveDataDay + "),'yyyymm') " +
                            "and t.partition_day =  TO_CHAR((sysdate-" + SubtractiveDataDay + "),'dd')) IDDINC " +
                            "INNER join (select 1 sl,(' and Intl Out: ' || to_char(round(sum(t.duration_float),0),'999,999,999,999')|| ' pm, ') IDDOutgoing from cdr_inter_ans_stat t " +
                            "where  t.transit_type = '30' AND t.billingcycle = TO_CHAR((sysdate-" + SubtractiveDataDay + "),'yyyymm') " +
                            "and t.partition_day =  TO_CHAR((sysdate-" + SubtractiveDataDay + "),'dd')) IDDOUTG on IDDINC.sl=IDDOUTG.sl) IDDALL " +
                            "INNER JOIN (SELECT 1 sl, (DMOUTG.DMOutgoing || DMINC.DMIncomming) DOM FROM " +
                            "(select 1 sl, (' Dmstc Out: ' || to_char(round(sum(t.duration_float),0),'999,999,999,999')|| ' pm') DMOutgoing from cdr_inter_ans_stat t " +
                            "where  t.transit_type = '31' AND t.billingcycle = TO_CHAR((sysdate-" + SubtractiveDataDay + "),'yyyymm') and t.partition_day =  TO_CHAR((sysdate-" + SubtractiveDataDay + "),'dd')) DMOUTG " +
                            "INNER join (select 1 sl,(' and In : ' || to_char(round(sum(t.duration_float),0),'999,999,999,999')|| ' pm') DMIncomming from cdr_inter_ans_stat t " +
                            "where  t.transit_type = '33' AND t.billingcycle = TO_CHAR((sysdate-" + SubtractiveDataDay + "),'yyyymm') and t.partition_day =  TO_CHAR((sysdate-" + SubtractiveDataDay + "),'dd')) DMINC on DMOUTG.sl=DMINC.sl) DOMALL ON DOMALL.sl= IDDALL.sl";


                        OracleDataReader reader = cmd.ExecuteReader();


                        while (reader.Read())
                        {
                            SMSData.Add("ANS", reader["SMSCONTENT"].ToString());
                        }
                        reader.Dispose();
                    }

                    if (_icx)
                    {
                        cmd.CommandText = "SELECT (IDDALL.INTALL || DOMALL.DOM) SMSCONTENT FROM " +
                            "(SELECT 1 sl,(INC.Incomming || OUTG.Outgoing) INTALL FROM " +
                            "(select 1 sl, ('ICX traffic for ' || TO_CHAR((sysdate-" + SubtractiveDataDay + "),'dd/mm') || ' Intl In: ' || to_char(round(sum(t.duration)/60,0),'999,999,999,999')|| ' pm') Incomming from cdr_inter_icx_stat t " +
                            "where  t.transit_type = '11' AND t.billingcycle = TO_CHAR((sysdate-" + SubtractiveDataDay + "),'yyyymm') " +
                            "and t.partition_day =  TO_CHAR((sysdate-" + SubtractiveDataDay + "),'dd')) INC " +
                            "INNER join (select 1 sl,(' and Out: ' || to_char(round(sum(t.duration)/60,0),'999,999,999,999')|| ' pm, ') Outgoing from cdr_inter_icx_stat t " +
                            "where  t.transit_type = '12' AND t.billingcycle = TO_CHAR((sysdate-" + SubtractiveDataDay + "),'yyyymm') " +
                            "and t.partition_day =  TO_CHAR((sysdate-" + SubtractiveDataDay + "),'dd')) OUTG on INC.sl=OUTG.sl) IDDALL " +
                            "INNER join (select 1 sl,(' DOM : ' || to_char(round(sum(t.duration)/60,0),'999,999,999,999')|| ' pm') DOM from cdr_inter_icx_stat t " +
                            "where  t.transit_type = '10' AND t.billingcycle = TO_CHAR((sysdate-" + SubtractiveDataDay + "),'yyyymm') " +
                            "and t.partition_day =  TO_CHAR((sysdate-" + SubtractiveDataDay + "),'dd')) DOMALL ON DOMALL.sl = IDDALL.sl";

                        OracleDataReader reader = cmd.ExecuteReader();


                        while (reader.Read())
                        {
                            string s = reader.GetName(0);
                            SMSData.Add("ICX", reader["SMSCONTENT"].ToString());
                        }
                        reader.Dispose();
                    }

                    if (_itx)
                    {
                        cmd.CommandText = "SELECT (INC.Incomming || OUTG.Outgoing) SMSCONTENT FROM " +
                            "(select 1 sl, ('IGW traffic for ' || TO_CHAR((sysdate-" + SubtractiveDataDay + "),'dd/mm') || ' In: ' || to_char(round(sum(t.duration_float),0),'999,999,999,999')|| ' pm') Incomming from cdr_inter_itx_stat t " +
                            "where  t.settle_scene = 'SC101' AND t.billingcycle = TO_CHAR((sysdate-" + SubtractiveDataDay + "),'yyyymm') " +
                            "and t.partition_day =  TO_CHAR((sysdate-" + SubtractiveDataDay + "),'dd')) INC " +
                            "INNER join (select 1 sl,(' and Out: ' || to_char(round(sum(t.duration_float),0),'999,999,999,999')|| ' pm') Outgoing from cdr_inter_itx_stat t " +
                            "where  t.TRANSIT_TYPE in ('20','21','22') AND t.billingcycle = TO_CHAR((sysdate-" + SubtractiveDataDay + "),'yyyymm') and t.partition_day =  TO_CHAR((sysdate-" + SubtractiveDataDay + "),'dd')) OUTG on INC.sl=OUTG.sl";

                        OracleDataReader reader = cmd.ExecuteReader();


                        while (reader.Read())
                        {
                            SMSData.Add("ITX", reader["SMSCONTENT"].ToString());
                        }
                        reader.Dispose();
                    }


                    cmd.Dispose();
                    conn.Dispose();

                    if (_itx == true && Error_CheckOrNot)
                    {
                        try
                        {
                            if (AcceptanceString == "")
                            {
                                incoming_acceptance_diff = 0.3;
                            }
                            else
                            {
                                incoming_acceptance_diff = Convert.ToDouble(AcceptanceString);
                            }
                        }
                        catch (Exception ex)
                        {
                            Show_LogTextblock(ex.Message);
                            incoming_acceptance_diff = 0.3;
                        }


                        IncomingDifferenceCheck();
                        OutgoingDifferenceCheck();                      


                        if (_generator == "AUTO")
                        {  
                            if (incoming_Diff_actual > incoming_acceptance_diff)
                            {
                                _itx = false;
                                if(_itx_err_send)
                                    send_error_msg = true;
                            }
                        }
                    }
                    

                    if (SMSData.Count == 0)
                    {
                        throw new Exception("Error in building SMS Content.");
                    }
                    else
                    {
                        SMSData_Status = 1;
                    }
                }
                catch (Exception ex)
                {
                    NumberofFailtoGenerateSMS++;
                    String s;
                    s = "Error in live database: " + ex.Message + ", Number of Attemt: " + NumberofFailtoGenerateSMS;
                    Write_logFile(s);////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

                    Show_LogTextblock(s);
                    Show_LogTextblock("Please make sure your PC is connected to oracle billing database.");
                    SMSData_Status = 0;

                    if (!Query_Only) //this block requires if retrying sms and query data in sleep time
                    {
                        if (Query_Only == false) q_flag = 1;

                        if (NumberofFailtoGenerateSMS <= 7)
                        {
                            TimeSpan t = TimeSpan.FromSeconds(100);
                            Thread.Sleep(t);
                        }
                        else
                        {
                            TimeSpan t = TimeSpan.FromSeconds(500);
                            Thread.Sleep(t);
                        }
                        if (q_flag == 1)
                        {
                            Query_Only = false;
                            q_flag = 0;
                        }
                    }
                }
                finally 
                {
                    if (SMSData_Status == 1)
                    {
                        NumberofFailtoGenerateSMS = 0;

                        Dispatcher.BeginInvoke((Action)(() =>
                        {
                            ReplyTextblock_clean();
                        }));

                        Show_LogTextblock("Generated SMS contents successfully.");

                        if (_ans == true)
                        {
                            Write_logFile(SMSData["ANS"]);

                            Dispatcher.BeginInvoke((Action)(() =>
                            {
                                _run3.Text = "ANS message:";
                                _run4.Text = "\n" + SMSData["ANS"] + "\n\n";
                            }));
                        }

                        if (_icx == true)
                        {
                            Write_logFile(SMSData["ICX"]);

                            Dispatcher.BeginInvoke((Action)(() =>
                            {
                                _run5.Text = "ICX message:";
                                _run6.Text = "\n" + SMSData["ICX"] + "\n\n";
                            }));
                        }


                        if(_itx_ == true)
                        {
                            Write_logFile(SMSData["ITX"]);

                            Dispatcher.BeginInvoke((Action)(() =>
                            {
                                _run7.Text = "ITX message:";
                                _run8.Text = "\n" + SMSData["ITX"];

                                if (Error_CheckOrNot)
                                {
                                    _run11.Text = "\n\nICX-ITX difference:";
                                    _run12.Text = "\tIncoming: " + incoming_Diff_actual.ToString("#0.00") + "%\tOutgoing: " + outgoing_Diff_actual.ToString("#0.00") + "%\tIncoming acceptance: " + incoming_acceptance_diff.ToString("#0.00") + "%";
                                    Write_logFile("ICX-ITX difference:\tIncoming: " + incoming_Diff_actual.ToString("#0.00") + "%\tOutgoing: " + outgoing_Diff_actual.ToString("#0.00") + "%\tIncoming acceptance: " + incoming_acceptance_diff.ToString("#0.00") + "%");

                                    if (incoming_Diff_actual > incoming_acceptance_diff && _generator == "AUTO")
                                    {
                                        ITX.IsChecked = false;
                                        Show_LogTextblock("ITX SMS suspended. Showing abnormal traffic. ICX-ITX difference is " + incoming_Diff_actual.ToString("#0.00") + "%, which is greater than " + incoming_acceptance_diff.ToString("#0.00") + "%.");
                                        Write_logFile("ITX SMS suspended due to abnormal traffic.");
                                        if (reportingPhnNumString.Length < 11 || !_itx_err_send)
                                        {
                                            Show_LogTextblock("No error report will be sent.");
                                        }
                                    }
                                }
                            }));
                            
                        }

                        if (!Query_Only)
                        {
                            //FetchingSMSDestination(); //Collects destination numbers

                            if (SMSDestination_Status == 1)
                            {
                                string str = "Phone numbers are read successfully.";
                                Show_LogTextblock(str);
                                if (_generator == "ADDITIONAL")
                                    SendITXAdditionalReport();
                                else
                                    Sending_SMS();

                                if (send_error_msg)
                                {
                                    send_error_msg = false;

                                    if (reportingPhnNumString.Length >= 11)
                                    {
                                        SendITXErrorReport();
                                    }
                                }
                            }
                        }
                    }
                    else if (!Query_Only && _reset == false)
                    {
                        FetchingSMSData(_generator);
                    }
                }
            }
        }

        private void IncomingDifferenceCheck()
        {
            OracleConnection conn = new OracleConnection(OracldbLive);  // C#
            conn.Open();
            OracleCommand cmd = new OracleCommand();
            cmd.CommandType = CommandType.Text;
            cmd.Connection = conn;

            cmd.CommandText = "select sum(t.DURATION_FLOAT) MINUTES from cdr_inter_itx_stat t where t.billingcycle = TO_CHAR((sysdate- " + SubtractiveDataDay + "),'yyyymm') and t.partition_day =  TO_CHAR((sysdate- " + SubtractiveDataDay + "),'dd') and t.settle_scene = 'SC101'";
            double itx_val = 0;
            OracleDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string s = "";
                s = reader["MINUTES"].ToString();
                if (s != "")
                    itx_val = Convert.ToDouble(s);
            }


            cmd.CommandText = "select sum(t.duration) MINUTES from cdr_inter_icx_stat t where t.billingcycle = TO_CHAR((sysdate- " + SubtractiveDataDay + "),'yyyymm') and t.partition_day =  TO_CHAR((sysdate- " + SubtractiveDataDay + "),'dd') and t.TRANSIT_TYPE = '11'";
            double icx_val = 0;
            reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string s = "";
                s = reader["MINUTES"].ToString();
                if (s != "")
                    icx_val = Convert.ToDouble(s);
            }
            icx_val = icx_val / 60;
            reader.Dispose();   

            cmd.Dispose();
            conn.Dispose();

            incoming_Diff_actual = (icx_val - itx_val) * 100 / icx_val;
        }

        private void OutgoingDifferenceCheck()
        {
            OracleConnection conn = new OracleConnection(OracldbLive);  // C#
            conn.Open();
            OracleCommand cmd = new OracleCommand();
            cmd.CommandType = CommandType.Text;
            cmd.Connection = conn;

            cmd.CommandText = "select sum(t.DURATION_FLOAT) MINUTES from cdr_inter_itx_stat t where t.billingcycle = TO_CHAR((sysdate- " + SubtractiveDataDay + "),'yyyymm') and t.partition_day =  TO_CHAR((sysdate- " + SubtractiveDataDay + "),'dd') and t.TRANSIT_TYPE in ('20','21','22')";
            double itx_val = 0;
            OracleDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string s = "";
                s = reader["MINUTES"].ToString();
                if (s != "")
                    itx_val = Convert.ToDouble(s);
            }


            cmd.CommandText = "select sum(t.duration) MINUTES from cdr_inter_icx_stat t where t.billingcycle = TO_CHAR((sysdate- " + SubtractiveDataDay + "),'yyyymm') and t.partition_day =  TO_CHAR((sysdate- " + SubtractiveDataDay + "),'dd') and t.TRANSIT_TYPE = '12'";
            double icx_val = 0;
            reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string s = "";
                s = reader["MINUTES"].ToString();
                if (s != "")
                    icx_val = Convert.ToDouble(s);
            }
            icx_val = icx_val / 60;
            reader.Dispose();

            cmd.Dispose();
            conn.Dispose();

            outgoing_Diff_actual = (itx_val - icx_val) * 100 / icx_val;
        }




        int q_flag = 0;

        private void FetchingSMSDestination()
        {
            if (SMSDestination_Status == 0)
            {
                try
                {
                    Dest_ANS.Clear();
                    Dest_ICX.Clear();
                    Dest_ITX.Clear();

                    NumberofDestination = 0;
                    string str = "";
                    System.IO.StreamReader file = new System.IO.StreamReader(@Destination_Number_url);
                    string[] data = new string[2];
                    while ((str = file.ReadLine()) != null)
                    {
                        data = str.Split(' ');

                        if (data[0] != "")
                        {
                            if (data[1] == "ANS" && _ans == true)
                            {
                                Dest_ANS.Add(data[0]);
                                NumberofDestination++;
                            }

                            if (data[1] == "ICX" && _icx == true)
                            {
                                Dest_ICX.Add(data[0]);
                                NumberofDestination++;
                            }

                            if (data[1] == "ITX" && _itx == true)
                            {
                                Dest_ITX.Add(data[0]);
                                NumberofDestination++;
                            }
                        }
                    }

                    file.Close();

                    if (Dest_ANS.Count == 0 && Dest_ICX.Count == 0 && Dest_ITX.Count == 0)
                    {
                        throw new Exception("No destination number found in the file.");
                    }
                    else
                    {
                        SMSDestination_Status = 1;
                    }
                }
                catch (Exception ex)
                {
                    String s;
                    s = "Error in phone number reading: " + ex.Message;

                    Write_logFile(s);////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

                    Show_LogTextblock(s);

                    SMSDestination_Status = 0;

                    if (Destination_Number_url == "")
                    {
                        Show_LogTextblock("Please browse a correct text file for destination phone numbers.");
                        Dispatcher.BeginInvoke((Action)(() =>
                        {
                            Browse_Btn_Animation();
                            MessageBox.Show(ex.Message + " Please browse a text file for destination phone numbers. Each line of text file format: PHONE-NUMBER<Space>GROUP<Space>Person-Name(optional).\nAs for example:\n01XXXXXXXXX ANS Mr. Abc\n01XXXXXXXXX ICX\n01XXXXXXXXX ITX Mrs. Xyz", Title);
                        }));
                    }
                    else
                    {
                        Show_LogTextblock("Wrong text file selected. Please browse the correct text file again for destination phone numbers.");
                        Dispatcher.BeginInvoke((Action)(() =>
                        {
                            Browse_Btn_Animation();
                            MessageBox.Show("Wrong text file selected. Please browse the correct one. Each line of text file format: PHONE-NUMBER<Space>GROUP<Space>Person-Name(optional).\nAs for example:\n01XXXXXXXXX ANS Mr. Abc\n01XXXXXXXXX ICX\n01XXXXXXXXX ITX Mrs. Xyz", Title);
                        }));
                    }
                }
            }
        }

        String[] Error_report_PhnNum = new String[10];
        private void SendITXErrorReport()
        {
            Error_report_PhnNum = reportingPhnNumString.Split(',');
            SentMsgCount = 0;
            NumberofDestination = Error_report_PhnNum.Length;
            Send_Status = 0;
            Show_LogTextblock("Sending ITX error report to the following numbers: " + reportingPhnNumString);
            Write_logFile("Sending ITX error report to the following number(s): " + reportingPhnNumString);
            log_status_print = false;
            string Errorstring;
            
            Errorstring = "IGW showing abnormal traffic. " + incoming_Diff_actual.ToString("#0.00") + "% less than ICX, SMS suspended to high officials.\n" + SMSData["ITX"];

            foreach (var num in Error_report_PhnNum)
            {
                HttpCallforSMS(num, "ITX", Errorstring);
            }
        }


        String[] AdditionalITX_PhnNum = new String[10];
        private void SendITXAdditionalReport()
        {
            AdditionalITX_PhnNum = AdditionalITXPhnNumString.Split(',');
            SentMsgCount = 0;
            NumberofDestination = AdditionalITX_PhnNum.Length;
            Send_Status = 0;
            Show_LogTextblock("Sending ITX Additional report.");
            Write_logFile("Sending ITX Additional report to the following number(s): " + AdditionalITXPhnNumString);
            log_status_print = false;

            foreach (var num in AdditionalITX_PhnNum)
            {
                HttpCallforSMS(num, "ITX", SMSData["ITX"]);
            }
        }

        private void Sending_SMS()
        {
            if (Send_Status == 1)
            {
                Show_LogTextblock("SMS already sent today: " + DateTime.Now.ToShortDateString());
            }
            else
            {
                Show_LogTextblock("SMS sending, please wait..... .... ... .. .");

                log_status_print = false; // "SMS already sent today" this message will be shown in log if manually send message when retrying. This status is using to show it only once, not for each message

                if (_ans)
                {
                    foreach (var num in Dest_ANS)
                    {
                        if (num != "")
                            HttpCallforSMS(num, "ANS", SMSData["ANS"]); //sending for ANS
                    }
                }

                if (_icx)
                {
                    foreach (var num in Dest_ICX)
                    {
                        if (num != "")
                            HttpCallforSMS(num, "ICX", SMSData["ICX"]); //sending for ICX
                    }
                }

                if (_itx)
                {
                    foreach (var num in Dest_ITX)
                    {
                        if (num != "")
                            HttpCallforSMS(num, "ITX", SMSData["ITX"]); //sending for ITX
                    }
                }
            }
        }

        int SentMsgCount = 0, NumberofFailtoSendSMS = 0;
        private void HttpCallforSMS(string PhnNum, String choice, String SmsString)
        {
            if (Send_Status == 0)
            {
                try
                {
                    String UrlString = "http://bulksms.teletalk.com.bd/link_sms_send.php?op=SMS&user=" + User_name_string + "&pass=" + Password_string + "&mobile=" + PhnNum + "&sms=" + SmsString;//#############################################################
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(@UrlString);
                    request.AllowWriteStreamBuffering = false;
                    WebResponse response = request.GetResponse();

                    // Display the status.
                    //Console.WriteLine(((HttpWebResponse)response).StatusDescription);
                    // Get the stream containing content returned by the server.
                    Stream dataStream = response.GetResponseStream();
                    // Open the stream using a StreamReader for easy access.
                    StreamReader reader = new StreamReader(dataStream);
                    // Read the content.
                    responseFromHttpWeb = reader.ReadToEnd();
                    // Display the content.
                    //Console.WriteLine(responseFromServer);


                    CreditStatus_Text = "";

                    if (((HttpWebResponse)response).StatusDescription == "OK")
                    {
                        SentMsgCount++;
                        if (SentMsgCount == NumberofDestination)
                        {
                            string[] data = new string[7];
                            data = responseFromHttpWeb.Split(',');
                            foreach (var x in data)
                            {
                                if (x.Contains("CURRENT CREDIT"))
                                {
                                    CreditStatus_Text = x;
                                    break;
                                }
                                if (x.Contains("FAILED"))
                                {
                                    throw new Exception("ERROR: FROM TELETALK SERVER");
                                }
                            }

                            string str = "Sent SMS today, total number of SMS: " + NumberofDestination.ToString() + ", " + DateTime.Now.ToLongDateString();
                            Show_LogTextblock(str);

                            string[] data2 = new string[2];
                            data2 = CreditStatus_Text.Split('=');


                            double d = 0;
                            try
                            {
                                d = Convert.ToDouble(data2[1]);
                            }
                            catch (Exception ex)
                            {
                                Write_logFile("Error in HTTP response conversion: " + ex.Message);
                            }

                            if (Balance_CheckOrNot)
                            {
                                if (d > 0)
                                {
                                    CreditStatus_Today = Convert.ToInt32(d);

                                    if (CreditStatus_Yesterday == 0)
                                    {
                                        CreditStatus_Yesterday = CreditStatus_Today + NumberofDestination;
                                    }

                                    string replystring = "CURRENT CREDIT = ";
                                    replystring += CreditStatus_Today.ToString();

                                    if (CreditDeducted_Yesterday != 0)
                                    {
                                        replystring += "\tCREDIT deducted yesterday = ";
                                        replystring += CreditDeducted_Yesterday.ToString();
                                    }

                                    //if (CreditStatus_Yesterday != 0)
                                    //{
                                    //    replystring += "\tCREDIT shown yesterday = ";
                                    //    replystring += CreditStatus_Yesterday.ToString();

                                    //    if (CreditStatus_Yesterday - CreditStatus_Today >= 0)
                                    //    {
                                    //        replystring += "\tDeducted in one day = ";
                                    //        replystring += (CreditStatus_Yesterday - CreditStatus_Today).ToString();
                                    //    }
                                    //}    
                                    

                                    Dispatcher.BeginInvoke((Action)(() =>
                                    {
                                        _run1.Text = "SMS Unit Balance in Teletalk account: " + DateTime.Now.ToString() + "\n";
                                        _run2.Text = replystring;
                                    }));
                                }
                                else
                                {
                                    Dispatcher.BeginInvoke((Action)(() =>
                                    {
                                        _run1.Text = "Reply from Teletalk Server: " + DateTime.Now.ToString() + "\n";
                                        _run2.Text = responseFromHttpWeb;
                                    }));
                                }
                            }

                            Write_logFile("Sent SMS today, total number of SMS: " + NumberofDestination.ToString() + "."); ///////////////////////////////////////////////////////////////

                            Send_Status = 1;
                            SentMsgCount = 0;
                            NumberofFailtoSendSMS = 0;
                        }
                    }

                    // Clean up the streams.
                    reader.Close();
                    dataStream.Close();
                    response.Close();
                    

                    reader.Dispose();
                    dataStream.Dispose();
                    response.Dispose();
                }

                catch (Exception ex)
                {
                    if (ex.Message == "ERROR: FROM TELETALK SERVER")
                    {
                        //MessageBox.Show("Failed to send SMS, Invalid Phone Number.");
                        Show_LogTextblock("ERROR: FROM TELETALK SERVER, " + responseFromHttpWeb);
                        Write_logFile("ERROR: FROM TELETALK SERVER, " + responseFromHttpWeb);
                    }
                    else
                    {
                        NumberofFailtoSendSMS++;
                        String s = "Error in SMS sending: " + PhnNum + ":" + choice + ", " + ex.Message + ", Number of Attemt: " + NumberofFailtoSendSMS;
                        Write_logFile(s);/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

                        Show_LogTextblock(s);
                        Show_LogTextblock("Please make sure your PC is connected to internet.");


                        if (NumberofFailtoSendSMS <= 7)
                        {
                            TimeSpan t = TimeSpan.FromSeconds(100);
                            Thread.Sleep(t);
                        }
                        else
                        {
                            TimeSpan t = TimeSpan.FromSeconds(500);
                            Thread.Sleep(t);
                        }
                        _sleep_state = true;
                    }
                }
                finally //used only for retry option when awaking from sleep state
                {
                    if (Send_Status == 0 && _sleep_state == true)
                    {
                        _sleep_state = false;
                        if (_reset == false)
                        {
                            HttpCallforSMS(PhnNum, choice, SmsString);
                        }
                        else
                        {
                            Show_LogTextblock("Halted SMS sending after awaking from sleep.");
                            Write_logFile("Halted SMS sending after awaking from sleep.");
                        }
                    }
                }
            }
            else
            {
                if (log_status_print == false)
                {
                    Show_LogTextblock("SMS already sent today: " + DateTime.Now.ToShortDateString());
                    log_status_print = true;
                }
            }
        }

        bool log_status_print = false;
        bool _sleep_state = false;


        private void Button_Click_Send_SMS(object sender, RoutedEventArgs e)
        {
            Write_logFile("Button clicked (Send SMS).");/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            if (Send_Status == 1)
            {
                Show_LogTextblock("SMS already sent today: " + DateTime.Now.ToShortDateString());
                Write_logFile("SMS already sent today.");
                MessageBox.Show("SMS already sent for today. If you want to send again, at first click 'Reset' button.", Title);
            }
            else
            {
                if (MessageBox.Show("Do you really want to send message for traffic date: " + DateTime.Today.Subtract(TimeSpan.FromDays(SubtractiveDataDay)).ToShortDateString() + "?",
                    Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    SentMsgCount = 0;
                    SMSData_Status = 0;
                    //SMSDestination_Status = 0;
                    Query_Only = false;

                    if (_ans | _icx | _itx)
                    {
                        _reset = false;
                        ThreadHandle("MANUAL");
                    }
                    else
                    {
                        Show_LogTextblock("No message group is checked.");
                        MessageBox.Show("Please select at least one message group to send SMS.", Title);
                    }
                }
            }
            
        }


        private void Querey_Data_Click(object sender, RoutedEventArgs e)
        {
            Write_logFile("Button clicked (Querey Data).");/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            Query_Only = true;
            SMSData_Status = 0;

            if (_ans | _icx | _itx)
            {
                ReplyTextblock_clean();
                ThreadHandle("MANUAL");
            }
            else
            {
                Show_LogTextblock("No message group is checked.");
                MessageBox.Show("Please select at least one message group to see the traffic data.", Title);
            }
        }


        private void Button_Click_Reset(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Do you really want to reset today's status or stop any process?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _reset = true;
                try
                {
                    Write_logFile("Button clicked (Reset).");/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                    SentMsgCount = 0;
                    SMSData_Status = 0;
                    //SMSDestination_Status = 0;
                    Send_Status = 0;
                    NumberofDestination = 0;

                    //SubtractiveDataDay = 1;
                    //_date_picker.IsEnabled = false;
                    //calender_btn.Content = "Enable Calender";
                    //_date_picker.SelectedDate = DateTime.Today.Subtract(TimeSpan.FromDays(1));

                    ReplyTextblock_clean();
                    _run3.Text = "Instructions of using this app:";
                    _run4.Text = Show_Instructions();

                    log_textblock.Text = "# " + DateTime.Now.ToLongTimeString() + ":- Successfully cleared today's status.\n";
                }
                catch (Exception ex)
                {
                    Show_LogTextblock(ex.Message);
                    Write_logFile(ex.Message);
                }
            }
        }


        private void button_Click_Browse(object sender, RoutedEventArgs e)
        {
            try
            {
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

                // Set filter for file extension and default file extension
                dlg.DefaultExt = ".txt";
                dlg.Filter = "Text documents (.txt)|*.txt";

                // Display OpenFileDialog by calling ShowDialog method
                Nullable<bool> result = dlg.ShowDialog();

                // Get the selected file name and display in a TextBox
                if (result == true)
                {
                    // Open document
                    string filename = dlg.FileName;
                    Destination_Number_url = filename;
                    Show_LogTextblock("File has been selected successfully. Path is " + Destination_Number_url);

                    Browse_btn.ClearValue(Button.BackgroundProperty);

                    SMSDestination_Status = 0;

                    FetchingSMSDestination();

                    if (SMSDestination_Status == 1)
                    {
                        String num_list = "Phone numbers are:\n";

                        num_list += "\tANS Group:\n";
                        if (Dest_ANS.Count == 0) num_list += "\t\t Nil\n";
                        int i = 0, j = 0, k = 0;
                        foreach (var num in Dest_ANS)
                        {
                            if (num != "")
                            {
                                num_list += "\t\t" + num + "\n";
                                i++;
                            }
                        }

                        num_list += "\tICX Group:\n";
                        if (Dest_ICX.Count == 0) num_list += "\t\t Nil\n";
                        foreach (var num in Dest_ICX)
                        {
                            if (num != "")
                            {
                                num_list += "\t\t" + num + "\n";
                                j++;
                            }
                        }

                        num_list += "\tITX Group:\n";
                        if (Dest_ITX.Count == 0) num_list += "\t\t Nil\n";
                        foreach (var num in Dest_ITX)
                        {
                            if (num != "")
                            {
                                num_list += "\t\t" + num + "\n";
                                k++;
                            }
                        }
                        num_list += "-----:Successfully imported the file data:-----";
                        Show_LogTextblock(num_list);
                        Show_LogTextblock("Total phone numbers: " + NumberofDestination + "\n\tANS group: " + i + ",\tICX group: " + j + ",\tITX group: " + k);
                    }
                }
            }
            catch (Exception ex)
            {
                Show_LogTextblock(ex.Message);
                Write_logFile(ex.Message);
            }
        }



        private void credit_label_MouseEnter_1(object sender, MouseEventArgs e)
        {
            Popup1_1_textblock.Text = "Md. Rakib Subaid\nManager (Technical)\nBTCL, Sher-E-Bangla Nagar, Dhaka\nPhone: 01917300427";
            //Popup1_2_textblock.Text = "Bidyut Chandra Aich\nDivisional Engineer\nComputer & Data Center, BTCL\nSher-e-Bangla Nagar, Dhaka";
            Popup1_Credit.IsOpen = true;
            _bg_credit.Color = Colors.LightSkyBlue;
            //credit_label.Foreground = Brushes.Black;
            _credit_link.Foreground = Brushes.Red;
        }

        private void credit_label_MouseLeave_1(object sender, MouseEventArgs e)
        {
            Popup1_Credit.IsOpen = false;
            _bg_credit.Color = Colors.DarkBlue;
            //credit_label.Foreground = Brushes.Wheat;
            _credit_link.Foreground = Brushes.Wheat;
        }

        private void instruct_label_MouseEnter_1(object sender, MouseEventArgs e)
        {
            _run9.Text = "Instructions of using this app:";
            _run10.Text = Show_Instructions();
            Popup7_Instruct.IsOpen = true;
            _bg_instruct.Color = Colors.LightSkyBlue;
            Instruct_label.Foreground = Brushes.Black;
        }

        private void instruct_label_MouseLeave_1(object sender, MouseEventArgs e)
        {
            Popup7_Instruct.IsOpen = false;
            _bg_instruct.Color = Colors.DarkBlue;
            Instruct_label.Foreground = Brushes.Wheat;
        }

        private void Reset_MouseEnter_1(object sender, MouseEventArgs e)
        {
            Popup2_Reset.IsOpen = true;
            Popup2_Reset_textblock.Text = "Click here to reset app data, if you need to send SMS multiple times in a same day.\n(It will also abort any existing/running process.)";
        }

        private void Reset_MouseLeave_1(object sender, MouseEventArgs e)
        {
            Popup2_Reset.IsOpen = false;
        }

        private void SendBtn_MouseEnter_1(object sender, MouseEventArgs e)
        {
            Popup3_SendBtn.IsOpen = true;
        }

        private void SendBtn_MouseLeave_1(object sender, MouseEventArgs e)
        {
            Popup3_SendBtn.IsOpen = false;
        }

        private void Alarm_time_textbox_MouseEnter(object sender, MouseEventArgs e)
        {
            Popup4_AlarmTime_textblock.Text = "SMS will be sent at the time mentioned in this box.";
            Popup4_AlarmTime.IsOpen = true;
        }

        private void Alarm_time_textbox_MouseLeave(object sender, MouseEventArgs e)
        {
            Popup4_AlarmTime.IsOpen = false;
        }

        private void BrowseBtn_MouseEnter_1(object sender, MouseEventArgs e)
        {
            if (Destination_Number_url == "")
                Popup5_BrowseBtn_textblock.Text = "Please browse a text file for destination phone numbers.";
            else
                Popup5_BrowseBtn_textblock.Text = "Now phone number path is " + Destination_Number_url;

            Popup5_BrowseBtn.IsOpen = true;
        }

        private void BrowseBtn_MouseLeave_1(object sender, MouseEventArgs e)
        {
            Popup5_BrowseBtn.IsOpen = false;
        }

        private void CheckBox_MouseEnter_1(object sender, MouseEventArgs e)
        {
            Popup6_Checkbox.IsOpen = true;
        }

        private void CheckBox_MouseLeave_1(object sender, MouseEventArgs e)
        {
            Popup6_Checkbox.IsOpen = false;
        }

        private void calender_btn_MouseEnter_1(object sender, MouseEventArgs e)
        {
            Popup8_Calender_textblock.Text = "By default data for previous day of current date (data for yesterday) will be sent normally.\n" +
                "If you need to send data for a specific date, at first click the 'Reset' button then click here.\nNeed to reset each time if you need to send SMS multiple times in a day.";
            Popup8_Calender.IsOpen = true;
        }

        private void calender_btn_MouseLeave_1(object sender, MouseEventArgs e)
        {
            Popup8_Calender.IsOpen = false;
        }


        private void Query_btn_MouseEnter_1(object sender, MouseEventArgs e)
        {
            Popup9_Query.IsOpen = true;
        }

        private void Query_btn_MouseLeave_1(object sender, MouseEventArgs e)
        {
            Popup9_Query.IsOpen = false;
        }


        private void report_btn_Click_1(object sender, RoutedEventArgs e)
        {
            Popup10_Settings.IsOpen = false;
        }

        private void report_label_MouseDoubleClick_1(object sender, MouseButtonEventArgs e)
        {
            Popup10_Settings.IsOpen = true;
        }

        private void report_label_MouseEnter_1(object sender, MouseEventArgs e)
        {
            if (reportingPhnNumString == "")
            {
                Popup11_txtblk.Text = "No number is given yet to send ITX error message.\nDouble click to add phone numbers.";
            }
            else
            {
                Popup11_txtblk.Text = "ITX error message will be sent to: " + reportingPhnNumString + "\nDouble click to add or change phone numbers.";
            }
            Popup11_reportNum.IsOpen = true;
            _bg_report.Color = Colors.Red;
            report_label.Foreground = Brushes.Black;
        }

        private void report_label_MouseLeave_1(object sender, MouseEventArgs e)
        {
            Popup11_reportNum.IsOpen = false;
            _bg_report.Color = Colors.Lime;
            report_label.Foreground = Brushes.Azure;
        }

        private void TextBox_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            EventTime1 = Alarm_time_textbox.Text;
        }



        public string EventTime1 { get; set; }
        public string EventTime2 { get; set; }

        private void ANS_Checked_1(object sender, RoutedEventArgs e)
        {
            _ans = true;
            Show_LogTextblock("ANS group is checked.");
        }

        private void ICX_Checked_1(object sender, RoutedEventArgs e)
        {
            _icx = true;
            Show_LogTextblock("ICX group is checked.");
        }

        private void ITX_Checked_1(object sender, RoutedEventArgs e)
        {
            _itx = true;
            _itx_ = true;
            Show_LogTextblock("ITX group is checked.");
        }

        private void ANS_Unchecked_1(object sender, RoutedEventArgs e)
        {
            _ans = false;
            Show_LogTextblock("ANS group is unchecked.");
        }

        private void ICX_Unchecked_1(object sender, RoutedEventArgs e)
        {
            _icx = false;
            Show_LogTextblock("ICX group is unchecked.");
        }

        private void ITX_Unchecked_1(object sender, RoutedEventArgs e)
        {
            _itx = false;
            _itx_ = false;
            Show_LogTextblock("ITX group is unchecked.");
        }


        private void calender_btn_Click_1(object sender, RoutedEventArgs e)
        {
            if (_date_picker.IsEnabled == false)
            {
                _date_picker.IsEnabled = true;
                calender_btn.Content = "Disable Calender";
                Show_LogTextblock("Calender is enabled.");
                _date_picker.DisplayDateEnd = DateTime.Today;
            }
            else
            {
                _date_picker.IsEnabled = false;
                calender_btn.Content = "Enable Calender";
                SubtractiveDataDay = 1;
                _date_picker.SelectedDate = DateTime.Today.Subtract(TimeSpan.FromDays(1));

                Show_LogTextblock("Calender is disabled.");
            }
        }


        private void _date_picker_SelectedDateChanged_1(object sender, SelectionChangedEventArgs e)
        {
            var diff = DateTime.Today.Subtract(_date_picker.SelectedDate.Value);
            SubtractiveDataDay = diff.Days;
            if (SubtractiveDataDay > 1)
                Show_LogTextblock("Selected date is " + SubtractiveDataDay + " days before today.");
            else if (SubtractiveDataDay == 0)
                Show_LogTextblock("Selected day is today.");
            else if (SubtractiveDataDay == 1)
                Show_LogTextblock("Selected day is yesterday.");
        }


        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Show_LogTextblock(ex.Message);
                Write_logFile(ex.Message);
            }
        }

        private void report_textbox_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            reportingPhnNumString = report_textbox.Text;
        }


        string AcceptanceString = "";
        private void error_parcent_textchanged_1(object sender, TextChangedEventArgs e)
        {
            AcceptanceString = error_parcent_textbox.Text;
        }

        private void error_checkbox_Checked_1(object sender, RoutedEventArgs e)
        {
            Error_CheckOrNot = true;
            Show_LogTextblock("Application will check ITX error.");
        }

        private void error_checkbox_Unchecked_1(object sender, RoutedEventArgs e)
        {
            Error_CheckOrNot = false;
            Show_LogTextblock("Application will not check ITX error.");
        }

        private void MainWindow_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ITXErrorChecking")
            {
                if (Error_CheckOrNot)
                {
                    ITX_error_Send.IsEnabled = true;
                    report_textbox.IsEnabled = true;
                    error_parcent_textbox.IsEnabled = true;
                }
                else
                {
                    ITX_error_Send.IsEnabled = false;
                    ITX_error_Send.IsChecked = false;
                    report_textbox.IsEnabled = false;
                    error_parcent_textbox.IsEnabled = false;
                    _itx_err_send = false;
                }
            }

            if (e.PropertyName == "AccountBalanceChecking")
            {
                if (Balance_CheckOrNot)
                {
                    replyFromWeb_textblock.Visibility = Visibility.Visible;
                }
                else
                {

                    replyFromWeb_textblock.Visibility = Visibility.Collapsed;
                    _run1.Text = "";
                    _run2.Text = "";
                }
            }
        }


        private void ITX_Error_Send_Checked_1(object sender, RoutedEventArgs e)
        {
            _itx_err_send = true;
            Show_LogTextblock("ITX error report will be sent now.");
        }

        private void ITX_Error_Send_Unchecked_1(object sender, RoutedEventArgs e)
        {
            _itx_err_send = false;
            Show_LogTextblock("ITX error report will not be sent now.");
        }


        String User_name_string = "";
        String Password_string = "";

        private void acc_psw_PasswordChanged_1(object sender, RoutedEventArgs e)
        {
            Password_string = acc_psw.Password;
        }

        private void user_name_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            User_name_string = user_name.Text;
        }

        private void Account_check_Checked_1(object sender, RoutedEventArgs e)
        {
            Balance_CheckOrNot = true;
            Show_LogTextblock("System will check SMS credit balance.");
        }

        private void Account_check_Unchecked_1(object sender, RoutedEventArgs e)
        {
            Balance_CheckOrNot = false;
            Show_LogTextblock("System will not check SMS credit balance.");
        }

        private void AdditionalMsgTime_txtbox_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            EventTime2 = AdditionalMsgTime_txtbox.Text;
        }

        private void ITXAdditional_checkbox_Checked_1(object sender, RoutedEventArgs e)
        {
            Send_ITX_AdditionalMsg = true;
            Show_LogTextblock("ITX Additional message enabled.");
            Write_logFile("ITX Additional message enabled.");
            AdditionalMsgTime_txtbox.IsEnabled = true;
            AdditionalMsgPhnNumber_txtbox.IsEnabled = true;
            Additional_send_btn.IsEnabled = true;
        }

        private void AccTest_btn_Click(object sender, RoutedEventArgs e)
        {
            AccTest_Txtblk.Text = "Please wait......";
            AccountTestTask();
        }

        private async void AccountTestTask()
        {
            await Task.Run(() => HttpCalltoTeletalk());
        }

        private void HttpCalltoTeletalk()
        {
            string responseFromHttpWeb = "";

            if (NetworkInterface.GetIsNetworkAvailable())
            {
                try
                {
                    String UrlString = "http://bulksms.teletalk.com.bd/link_sms_send.php?op=SMS&user=" + User_name_string + "&pass=" + Password_string;
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(@UrlString);
                    request.AllowWriteStreamBuffering = false;


                    WebResponse response = request.GetResponse();
                    // Display the status.
                    //Console.WriteLine(((HttpWebResponse)response).StatusDescription);
                    // Get the stream containing content returned by the server.
                    Stream dataStream = response.GetResponseStream();
                    // Open the stream using a StreamReader for easy access.
                    StreamReader reader = new StreamReader(dataStream);
                    // Read the content.

                    responseFromHttpWeb = reader.ReadToEnd();
                    // Display the content.
                    //Console.WriteLine(responseFromServer);

                    if (((HttpWebResponse)response).StatusDescription == "OK")
                    {
                        responseFromHttpWeb = responseFromHttpWeb.ToUpper();
                        if (responseFromHttpWeb.Contains("INVALID USER") || responseFromHttpWeb.Contains("WRONG USER"))
                        {
                            Dispatcher.BeginInvoke((Action)(() =>
                            {
                                AccTest_Txtblk.Text = "Sorry! Account is invalid :(";
                            }));
                        }
                        else if (responseFromHttpWeb.Contains("EMPTY SMS"))
                        {
                            Dispatcher.BeginInvoke((Action)(() =>
                            {
                                AccTest_Txtblk.Text = "Congrats! Account is valid :)";
                            }));
                        }
                        else
                        {
                            Dispatcher.BeginInvoke((Action)(() =>
                            {
                                AccTest_Txtblk.Text = "Not sure!! :(";
                            }));
                            Show_LogTextblock(responseFromHttpWeb);
                            Write_logFile("NOT SURE!! " + responseFromHttpWeb);
                        }
                    }
                    else
                    {
                        Dispatcher.BeginInvoke((Action)(() =>
                        {
                            AccTest_Txtblk.Text = "Server not OK!";
                        }));
                        Show_LogTextblock(responseFromHttpWeb);
                        Write_logFile("Server not OK! " + responseFromHttpWeb);
                    }

                    // Clean up the streams.
                    reader.Close();
                    dataStream.Close();
                    response.Close();

                    reader.Dispose();
                    dataStream.Dispose();
                    response.Dispose();
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        AccTest_Txtblk.Text = "Network error! :(";
                    }));
                }
            }
            else
            {
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    AccTest_Txtblk.Text = "Network unplugged! :(";
                }));
            }
        }

        private void ITXAdditional_checkbox_Unchecked_1(object sender, RoutedEventArgs e)
        {
            Send_ITX_AdditionalMsg = false;
            Show_LogTextblock("ITX Additional message disabled.");
            Write_logFile("ITX Additional message disabled.");
            AdditionalMsgTime_txtbox.IsEnabled = false;
            AdditionalMsgPhnNumber_txtbox.IsEnabled = false;
            Additional_send_btn.IsEnabled = false;
        }

        private void AdditionalMsgPhnNumber_txtbox_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            AdditionalITXPhnNumString = AdditionalMsgPhnNumber_txtbox.Text;
        }

        private void Additional_send_btn_Click_1(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Do you really want to send today's ITX traffic data?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Write_logFile("Button clicked (Send SMS).");/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                SentMsgCount = 0;
                SMSData_Status = 0;
                //SMSDestination_Status = 0;
                Query_Only = false;
                SubtractiveDataDay = 0;
                Send_Status = 0;

                _reset = false;
                if (AdditionalMsgPhnNumber_txtbox.Text != "")
                {
                    ThreadHandle("ADDITIONAL");
                }
                else
                {
                    MessageBox.Show("Please give phone numbers.", Title);
                    Write_logFile("No phone number to send additional ITX traffic.");
                    Show_LogTextblock("No phone number to send additional ITX traffic.");
                }
            }
        }
    }
}