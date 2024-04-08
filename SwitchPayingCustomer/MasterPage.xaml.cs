using LSExtensionWindowLib;
using LSSERVICEPROVIDERLib;
using Patholab_Common;
using Patholab_Controls;
using Patholab_DAL_V1;
using Patholab_XmlService;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace SwitchPayingCustomerPages
{
    /// <summary>
    /// Interaction logic for MasterPage.xaml
    /// </summary>
    public partial class MasterPage : UserControl
    {



        const string  CALCULATE_DEBIT_EVENT= "Calculate Debit";
        const string  CALCULATE_DEBIT_SLIDE_EVENT= "Calculate slide Debit";

        public MasterPage ( )
        {
            InitializeComponent ( );
            txtOrderName.Text = "";
            txtOrderName.Focus ( );
            FirstFocus ( );
        }
        private void FirstFocus ( )
        {
            //First focus because nautius's bag
            _timerFocus = new Timer { Interval = 10000 };
            _timerFocus.Interval = 1000;
            _timerFocus.Tick += timerFocus_Tick;
            _timerFocus.Start ( );

        }

        void timerFocus_Tick ( object sender, EventArgs e )
        {
            txtOrderName.Focus ( );

            _timerFocus.Stop ( );

        }
        public MasterPage ( INautilusServiceProvider sp, INautilusProcessXML xmlProcessor, INautilusDBConnection _ntlsCon,
                          IExtensionWindowSite2 _ntlsSite, INautilusUser _ntlsUser )
        {




            if ( _ntlsUser.GetRoleName ( ).ToUpper ( ) == "DEBUG" ) Debugger.Launch ( );
            InitializeComponent ( );
            //     this.SetResourceReference(Control.BackgroundProperty, System.Drawing.Color.FromName("Control"));
            this.sp = sp;
            this.xmlProcessor = xmlProcessor;
            this._ntlsCon = _ntlsCon;
            this._ntlsSite = _ntlsSite;
            this._ntlsUser = _ntlsUser;

            parstPerType = new List<U_PARTS> ( );
        }


        #region Private fields

        private INautilusProcessXML xmlProcessor;
        private INautilusUser _ntlsUser;
        private IExtensionWindowSite2 _ntlsSite;
        private INautilusServiceProvider sp;
        private INautilusDBConnection _ntlsCon;
        private SDG_USER sdg;
        private DataLayer dal;
        private List<U_PARTS> parstPerType;
        public bool DEBUG;
        private List<PHRASE_HEADER> phraseHeaders;
        private U_ORDER_USER order;
        private Timer _timerFocus;

        #endregion


        public bool CloseQuery ( )
        {

            if ( dal != null ) dal.Close ( );

            return true;
        }

        public void Initilaize ( )
        {
            dal = new DataLayer ( );

            if ( DEBUG )
            {
                // dal.MockConnect();
                sdg = dal.FindBy<SDG_USER> ( x => x.SDG_ID == 266 ).SingleOrDefault ( );


            }
            else
                dal.Connect ( _ntlsCon );

            var customers = dal.GetAll<U_CUSTOMER>().OrderBy(c=>c.NAME).ToList();

            cmbCustomers.ItemsSource = customers;
            cmbCustomers.DisplayMemberPath = "NAME";
            cmbCustomers.SelectedValuePath = "U_CUSTOMER_ID";
            btnSwitch.IsEnabled = false;
            order = null;


        }
        private void btnSwitch_Click ( object sender, RoutedEventArgs e )
        {
            if ( order == null )
            {
                CustomMessageBox.Show ( "לא נבחרה הזמנה. אנא בחר/י הזמנה ונסה/נסי שנית" );
                txtOrderName.Focus ( );
                return;
            }
            else if ( cmbCustomers.SelectedValue == null )
            {
                CustomMessageBox.Show ( "הגורם המשלם לא תקין.אנא בחר/י גורם משלם תקין ונסה/נסי שנית" );
                cmbCustomers.Focus ( );
                return;
            }
            else if ( order.U_CUSTOMER == ( long ) cmbCustomers.SelectedValue )
            {
                CustomMessageBox.Show ( "הגורם המשלם לא השתנה.אנא בחר/י גורם משלם אחר ונסה/נסי שנית" );
                return;
            }
            else
            {
                //write to sdg_log
                long Customer_id=-1;
                if ( order.U_CUSTOMER != null ) Customer_id = ( long ) order.U_CUSTOMER;
                long sessionId =(long)_ntlsCon.GetSessionId();
                if ( sdg != null ) dal.InsertToSdgLog ( sdg.SDG_ID, "SwitchPaying Customer", sessionId, Customer_id.ToString ( ) + "=>" + cmbCustomers.SelectedValue.ToString ( ) );
                //switch payment
                order.U_CUSTOMER = ( long ) cmbCustomers.SelectedValue;
                //save
                dal.SaveChanges ( );

                //find debit that are in status !=X and cancel them 
                U_DEBIT_USER[] debits =
                    dal.FindBy<U_DEBIT_USER>(du=> du.U_ORDER_ID ==order.U_ORDER_ID && du.U_DEBIT_STATUS!="X").ToArray();
                foreach ( U_DEBIT_USER debit in debits )
                {
                    debit.U_DEBIT_STATUS = "X";


                }
                ALIQUOT_USER[] aliquotUserArray =
                       dal.FindBy<ALIQUOT_USER>(
                           au =>
                           au.ALIQUOT.SAMPLE.SDG.SDG_USER.U_ORDER.U_ORDER_ID == order.U_ORDER_ID &&
                           (au.U_CALCULATE_DEBIT != "T" || au.U_CALCULATE_DEBIT ==null|| au.U_DEBIT!= null) ).ToArray();
                foreach ( ALIQUOT_USER aliquotUser in aliquotUserArray )
                {
                    aliquotUser.U_CALCULATE_DEBIT = "T";
                    aliquotUser.U_DEBIT_ID = null;
                }
                dal.SaveChanges ( );

                //make new debits
                if ( sdg != null )
                {
                    //for sdg
                    var fireEvent = new FireEventXmlHandler(sp);
                    fireEvent.CreateFireEventXml ( "SDG", sdg.SDG_ID, "Calculate Debit" );
                    var success = fireEvent.ProcssXml();
                    //for slides
                    SAMPLE[] samples = sdg.SDG.SAMPLEs.ToArray();
                    foreach ( SAMPLE sample in samples )
                    {
                        ALIQUOT_USER[] aliqs = sample.ALIQUOTs.Select(a => a.ALIQUOT_USER).ToArray();
                        foreach ( ALIQUOT_USER aliq in aliqs )
                        {
                            if ( aliq.U_COLOR_TYPE != null )
                            {

                                string eventName="";
                                if ( aliq.ALIQUOT.EVENTS.Contains ( CALCULATE_DEBIT_EVENT ) )
                                {
                                    eventName = CALCULATE_DEBIT_EVENT;
                                    NewMethod ( aliq, eventName );
                                }
                                else if ( aliq.ALIQUOT.EVENTS.Contains ( CALCULATE_DEBIT_SLIDE_EVENT ) )
                                {
                                    eventName = CALCULATE_DEBIT_SLIDE_EVENT;
                                    NewMethod ( aliq, eventName );
                                }
                                else
                                {   }
                                                                       
                                }

                                

                                }

                            }

                        
                    }
                    // ALIQUOT_USER[] slides = dal.FindBy<ALIQUOT_USER>(au => au.U_COLOR_TYPE != null).ToArray();
                

                CustomMessageBox.Show ( "הגורם המשלם השתנה בהצלחה!" );
                btnSwitch.IsEnabled = false;
                order = null;
                txtOrderName.Text = "";
                fillTxtDetails ( );


                return;

            }
        }

        private void NewMethod ( ALIQUOT_USER aliq, string eventName )
        {
            var calcAliq = new FireEventXmlHandler(sp);


            calcAliq.CreateFireEventXml ( "ALIQUOT", aliq.ALIQUOT_ID, eventName );
            var success2 = calcAliq.ProcssXml();
            if ( !success2 )
            {
                Logger.WriteLogFile ( calcAliq.ErrorResponse );
            }
        }

        private void txtOrderName_KeyDown ( object sender, KeyEventArgs e )
        {

            try
            {
                if ( e.Key != Key.Enter && e.Key != Key.Return )
                {
                    return;
                }
                else
                if ( txtOrderName.Text == "" )
                {
                    return;
                }
                else
                {
                    order = dal.FindBy<U_ORDER_USER> ( o => o.U_ORDER.NAME == txtOrderName.Text ).SingleOrDefault ( );
                    if ( order == null )
                    {
                        try
                        {
                            order = dal.FindBy<SDG_USER> ( d => d.U_PATHOLAB_NUMBER == txtOrderName.Text ).SingleOrDefault ( ).U_ORDER.U_ORDER_USER;
                        }
                        catch ( Exception )
                        {

                            order = null;
                        }
                        if ( order == null )
                        {
                            txtOrderName.Text = "";
                            fillTxtDetails ( );
                            CustomMessageBox.Show ( "ההזמנה/המקרה לא נמצאה, אנא נסו שנית." );
                            txtOrderName.Focus ( );
                        }
                    }
                    if ( order != null )
                    {
                        //load order

                        if ( order.U_CUSTOMER != null ) cmbCustomers.SelectedValue = order.U_CUSTOMER;
                        btnSwitch.IsEnabled = true;
                        fillTxtDetails ( );
                    }

                }

            }
            catch ( Exception ex )
            {
                CustomMessageBox.Show ( "אירעה תקלה" );
                Logger.WriteLogFile ( ex );
            }
        }
        private void fillTxtDetails ( )
        {
            txtDetails.Text = "";
            if ( order != null )
            {

                txtDetails.Text = "שם ההזמנה: " + order.U_ORDER.NAME + "\n";
                if ( order.U_CUSTOMER != null )
                {
                    txtDetails.Text += "שם הלקוח: " + order.U_CUSTOMER1.NAME + "\n";
                }
                txtDetails.Text += "שם הדרישה: ";
                //get the last sdg connected to the order in case of revision
                sdg = dal.FindBy<SDG_USER> ( du => du.U_ORDER_ID == order.U_ORDER_ID ).OrderByDescending ( du => du.SDG_ID ).FirstOrDefault ( );
                if ( sdg != null )
                {
                    txtDetails.Text += sdg.SDG.NAME + "\n";
                    txtDetails.Text += "מספר פתולאב:";
                    if ( sdg.U_PATHOLAB_NUMBER != null ) txtDetails.Text += sdg.U_PATHOLAB_NUMBER + "\n";
                }
            }
        }

        private void cmbCustomers_SelectionChanged ( object sender, SelectionChangedEventArgs e )
        {

        }

        private void txtOrderName_TouchEnter ( object sender, TouchEventArgs e )
        {

        }

        private void UserControl_GotFocus ( object sender, RoutedEventArgs e )
        {
            txtOrderName.Focus ( );
        }

        private void Button_Click ( object sender, RoutedEventArgs e )
        {
            txtOrderName.Focus ( );
        }

        private void txtOrderName_TextChanged ( object sender, TextChangedEventArgs e )
        {

        }







    }
}