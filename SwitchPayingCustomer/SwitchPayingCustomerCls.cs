using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using LSExtensionWindowLib;
using LSSERVICEPROVIDERLib;
using Patholab_Common;
using Patholab_DAL_V1;


namespace SwitchPayingCustomerPages
{
    [ComVisible(true)]
    [ProgId("SwitchPayingCustomer.SwitchPayingCustomerCls")]
    public partial class SwitchPayingCustomerCls : UserControl, IExtensionWindow
    {


        #region Private fields
        private INautilusProcessXML xmlProcessor;
        private INautilusUser _ntlsUser;
        private IExtensionWindowSite2 _ntlsSite;
        private INautilusServiceProvider sp;
        private INautilusDBConnection _ntlsCon;

        private DataLayer dal;
        public bool DEBUG;

        #endregion
        public SwitchPayingCustomerCls()
        {
            InitializeComponent();
            BackColor = Color.FromName("Control");
           

        }

        public bool CloseQuery()
        {
            if (w != null) w.CloseQuery();
            this.Dispose();
            return true;
        }

        public WindowRefreshType DataChange()
        {
            return LSExtensionWindowLib.WindowRefreshType.windowRefreshNone;

        }

        public WindowButtonsType GetButtons()
        {
            return LSExtensionWindowLib.WindowButtonsType.windowButtonsNone;


        }

        public void Internationalise()
        {
      
        }

        public void PreDisplay()
        {
            xmlProcessor = Utils.GetXmlProcessor(sp);

            _ntlsUser = Utils.GetNautilusUser(sp);
            if (_ntlsUser.GetRoleName().ToUpper() == "MANAGER" || _ntlsUser.GetRoleName().ToUpper() == "SYSTEM" || _ntlsUser.GetRoleName().ToUpper() == "DEBUG")
            {
                InitializeData();
            }
            else
            {
                MessageBox.Show("Only Managers can use this screen. Try using a different role in File menu -> Change Role, Or Contact Support");
            }
        }


        public void RestoreSettings(int hKey)
        {
        }

        public bool SaveData()
        {
            return true;
        }

        public void SaveSettings(int hKey)
        {
        }

        public void SetParameters(string parameters)
        {
        }

        public void SetServiceProvider(object serviceProvider)
        {
            sp = serviceProvider as NautilusServiceProvider;
            _ntlsCon = Utils.GetNtlsCon(sp);
        }

        public void SetSite(object site)
        {
            //todo:set site
        }

        public void Setup()
        {
        }



        public WindowRefreshType ViewRefresh()
        {
            return LSExtensionWindowLib.WindowRefreshType.windowRefreshNone;
        }

        public void refresh()
        {
            //throw new NotImplementedException();
        }

        private MasterPage w;
        private void InitializeData()
        
        
        {
           
            w = new MasterPage(sp, xmlProcessor, _ntlsCon, _ntlsSite, _ntlsUser);
            elementHost1.Child = w;
            w.Initilaize();
            w.Focus();
            w.txtOrderName.Focus();
            //dal = new DataLayer();
            //dal.Connect(_ntlsCon);

            //var sdgn = dal.GetAll<SDG>().Select(x => x.NAME);

            //foreach (var a in sdgn)
            //{
            // //   listBox1.Items.Add(a);
            //}
        }



    }
}
