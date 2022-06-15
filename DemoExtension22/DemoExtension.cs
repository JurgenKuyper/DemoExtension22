using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using DemoExtension22;
using Thrift.Collections;
using Yaskawa.Ext.API;
using Version = Yaskawa.Ext.Version;

namespace DemoExtension22
{
    class DemoExtension
    {
        public DemoExtension()
        {
            Yaskawa.Ext.Version version = new Yaskawa.Ext.Version(1, 0, 0); // set extension version
            var languages = new HashSet<string> {"en", "ja"}; // set supported languages
            
            if (RuntimeInformation.ProcessArchitecture == Architecture.X64) //switch according to processor architecture
            {
                extension = new Yaskawa.Ext.Extension("com.yaskawa.yeu.demotestextension.ext", // register new extension with development settings in pendant
                    version, "Yaskawa", languages, "10.0.0.4", 10080);
            }
            else
            {
                extension = new Yaskawa.Ext.Extension("com.yaskawa.yeu.demoextension.ext", // register new extension with "release" settings in pendant
                    version, "Yaskawa", languages, "10.0.0.4", 10080);
            }
    
    
            // The version of the API supported by the Smart Pendant on which we're running
            apiVersion = extension.apiVersion();
            Console.WriteLine("SP API version: "+apiVersion);
            //  (if we wish to support backward-compatability, it is possible we're linked
            //   against a Java client jar that corresponds to a newer API than the SP we're running on,
            //   so we'll need to check the version to ensure we don't use an API function that
            //   isn't supported, unless installed by a YIP package where we
            //   specified the minimum API version we need)
            pendant = extension.pendant();
            
            extension.subscribeLoggingEvents(); // receive logs from pendant
            extension.copyLoggingToStdOutput = true; // print log() to output
            extension.outputEvents = true; // print out events received
            controller = extension.controller();
            Console.WriteLine("Controller software version:" + controller.softwareVersion()); // log software version
            Console.WriteLine(" monitoring? " + controller.monitoring()); // only monitoring or able to change functions?     
            Console.WriteLine("Current language:" + pendant.currentLanguage()); // pendant language ISO 693-1 code
            Console.WriteLine("Current locale:" + pendant.currentLocale()); // log used locale
        }
        public void setup()
        { // query current language of pendant
            Version requiredMinimumApiVersion = new Version(2, 1, 0); // evaluate running apiversion with minimum required version
            if (apiVersion.Nmajor.CompareTo(requiredMinimumApiVersion.Nmajor) >= 0 &&
                apiVersion.Nminor.CompareTo(requiredMinimumApiVersion.Nminor) >= 0 &&
                apiVersion.Npatch.CompareTo(requiredMinimumApiVersion.Npatch) >= 0)
                dispNoticeEnabled = true;
            lang = pendant.currentLanguage();
            localeName = pendant.currentLocale();
            try {
                Console.WriteLine("lang_bundle_loaded",localeName);
            } catch (Exception ex) {
                // in case we don't have a translation for that language, fall-back to English
                Console.WriteLine("Language bundle for "+localeName+" not found - defaulting to English.");
            }
    
    
            controller.subscribeEventTypes(new THashSet<ControllerEventType> 
                {
                    ControllerEventType.PermissionGranted,
                    ControllerEventType.PermissionRevoked,
                    ControllerEventType.OperationMode,
                    ControllerEventType.ServoState,
                    ControllerEventType.ActiveTool,
                    ControllerEventType.PlaybackState,
                    ControllerEventType.RemoteMode,
                    ControllerEventType.IOValueChanged
                });
            THashSet<string> perms = new THashSet<string>(); // request and add networking permission
            perms.Add("networking");
            controller.requestPermissions(perms);
    
            pendant.subscribeEventTypes(new THashSet<PendantEventType>
                {
                    PendantEventType.SwitchedScreen,
                    PendantEventType.UtilityOpened,
                    PendantEventType.UtilityClosed,
                    PendantEventType.PanelOpened,
                    PendantEventType.PanelClosed,
                    PendantEventType.PopupOpened,
                    PendantEventType.PopupClosed
                });
            pendant.registerImageFile("images/MotoMINI_InHand.png");
            pendant.registerImageFile("images/fast-forward-icon.png");
            pendant.registerImageFile("images/d-icon-256.png");
            pendant.registerImageFile("images/d-icon-lt-256.png");
    
    
            // if support for multiple languages is anticipated, it is good
            //  practice to seperate help HTML files into subdirectories
            //  named by ISO language codes, like "en", "de" etc. which can
            //  be selected based on the pendant's currently set language
            String helpFile = "help/"+lang+"/something-help.html";
            FileAttributes attr = File.GetAttributes(helpFile);
            // check lang file exists, and if not, fall-back to English version
            if (!(File.Exists(helpFile) && !attr.HasFlag(FileAttributes.Directory))) // non-existent
                helpFile = "help/en/something-help.html";
    
            pendant.registerHTMLFile(helpFile);
    
    
            // Register all our YML files
            //  (while everything may be in a single file, good practice
            //   to break things up into smaller reusable parts)
            List<string> ymlFiles = new List<string>
            {
                "ControlsTab.yml",
                "ChartsTab.yml",
                "LayoutTab.yml",
                "AccessTab.yml",
                "NavTab.yml",
                "NetworkTab.yml",
                "EventsTab.yml",
                "LocalizationTab.yml",
                "UtilWindow.yml",
                "NavPanel.yml"
            };
            foreach(var ymlFile in ymlFiles)
                pendant.registerYMLFile(ymlFile);
    
    
            // A Utility window
            pendant.registerUtilityWindow("demoWindow",    // id
                                          "UtilWindow",    // Item type
                                          "Demo Extension",// Menu name
                                          "Demo Utility"); // Window title
    
            // A Navigatio panel (main programming screen)
            pendant.registerIntegration("navpanel", // id
                                        IntegrationPoint.NavigationPanel, // where
                                        "NavPanel", // YML Item type
                                        "Demo",     // Button label
                                        "images/d-icon-256.png"); // Button icon
    
            // place a button with icon on each jogging panel integration point
            //  (may have icon and/or short label, but width is limited)
            String jogPanelIconLight = "images/d-icon-lt-256.png";
            String jogPanelIconDark = "images/d-icon-256.png";
            //                          id                 where displayed                                      label    icon
            pendant.registerIntegration("jogTopLeft",      IntegrationPoint.SmartFrameJogPanelTopLeft,      "", "TPL",   jogPanelIconLight);
            pendant.registerIntegration("jogTopRight",     IntegrationPoint.SmartFrameJogPanelTopRight,     "", "TPR",   jogPanelIconLight);
            pendant.registerIntegration("jogBottomLeft",   IntegrationPoint.SmartFrameJogPanelBottomLeft,   "", "BTL",   jogPanelIconDark);
            pendant.registerIntegration("jogBottomCenter", IntegrationPoint.SmartFrameJogPanelBottomCenter, "", "BTCTR", jogPanelIconDark);
            pendant.registerIntegration("jogBottomRight",  IntegrationPoint.SmartFrameJogPanelBottomRight,  "", "BTR",   jogPanelIconDark);
            // unlike the integration points above, which only show when the jogging mode is 'smart frame', this one
            //  remains for all jogging modes:
            pendant.registerIntegration("JogTopCenter",    IntegrationPoint.JogPanelTopCenter,              "", "TOP",   jogPanelIconLight);
    
            pendant.registerIntegration("test2",    IntegrationPoint.SmartFrameJogPanelBottomAny,              "", "ANY",   jogPanelIconLight);
    
            // call onControlsItemClicked() for buttons on ControlsTab
            pendant.addItemEventConsumer("successbutton", PendantEventType.Clicked, onControlsItemClicked);
            pendant.addItemEventConsumer("noticebutton", PendantEventType.Clicked, onControlsItemClicked);
    
    
            // call onJogPanelButtonClicked() (below) if any jogging panel button clicked
            List<string> jogPanelList = new List<string>
            {
                "jogTopLeft",
                "jogTopRight",
                "jogBottomLeft",
                "jogBottomCenter",
                "jogBottomRight",
                "JogTopCenter"
            };
            foreach(var id in jogPanelList)
                pendant.addItemEventConsumer(id, PendantEventType.Clicked, onJogPanelButtonClicked);
    
    
            // call onEventsItemClicked() for various events from Items on the Events tab
            pendant.addItemEventConsumer("eventbutton1", PendantEventType.Clicked, onEventsItemClicked);
            pendant.addItemEventConsumer("eventtextfield1", PendantEventType.TextEdited, onEventsItemClicked);
            pendant.addItemEventConsumer("eventtextfield1", PendantEventType.EditingFinished, onEventsItemClicked);
            pendant.addItemEventConsumer("eventcombo1", PendantEventType.Activated, onEventsItemClicked);
            pendant.addItemEventConsumer("popupquestion", PendantEventType.Clicked, onEventsItemClicked);
    
            // for Popup Dialog Closed events (all popups)
            pendant.addEventConsumer(PendantEventType.PopupClosed, onEventsItemClicked);
    
            // Handle events from Layout tab
            pendant.addItemEventConsumer("row1spacingup", PendantEventType.Clicked, onLayoutItemClicked);
            pendant.addItemEventConsumer("row1spacingdown", PendantEventType.Clicked, onLayoutItemClicked);
    
            // Network tab
            pendant.addItemEventConsumer("networkSend", PendantEventType.Clicked, onNetworkSendClicked);
    
            // Navigation Panel
            pendant.addItemEventConsumer("instructionSelect", PendantEventType.Activated, onInsertInstructionControls);
            pendant.addItemEventConsumer("instructionText", PendantEventType.EditingFinished, onInsertInstructionControls);
            pendant.addItemEventConsumer("insertInstruction", PendantEventType.Clicked, onInsertInstructionControls);
            
        }
    
    
        // handy method to get the message from an Exception
        private static string ExtractBracketed(string str)
        {
            string s;
            if (str.IndexOf('<') > -1) //using the Regex when the string does not contain <brackets> returns an empty string.
                s = Regex.Match(str, @"\<([^>]*)\>").Groups[1].Value;
            else
                s = str; 
            if (s == "")
                return  "'Emtpy'"; //for log visibility we want to know if something it's empty.
            else
                return s;

        }

        public static string ThreadAndDateInfo
        {
            //returns thread number and precise date and time.
            get { return "[" + Thread.CurrentThread.ManagedThreadId + " - " + DateTime.Now.ToString("dd/MM HH:mm:ss.ffffff") + "] "; }
        }
        static String exceptionMessage(Exception e)
        {
            MethodBase site = e.TargetSite;//Get the methodname from the exception.
            string methodName = site == null ? "" : site.Name;//avoid null ref if it's null.
            string className = null;
            methodName = ExtractBracketed(methodName);
            System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(e, true); ;
            for (int i = 0; i < 3; i++)
            {
                //In most cases GetFrame(0) will contain valid information, but not always. That's why a small loop is needed. 
                var frame = trace.GetFrame(i);
                int lineNum = frame.GetFileLineNumber();//get the line and column numbers
                int colNum = frame.GetFileColumnNumber();
                className = ExtractBracketed(frame.GetMethod().ReflectedType.FullName);
                Trace.WriteLine(ThreadAndDateInfo + "Exception: " + className + "." + methodName + ", Ln " + lineNum + " Col " + colNum + ": " + e.Message);
                if (lineNum + colNum > 0)
                    break; //exit the for loop if you have valid info. If not, try going up one frame...
            }
            return e.ToString();
        }
    
    
        void onControlsItemClicked(PendantEvent e)
        {
            try {
    
                var props = e.Props;
                if (props.ContainsKey("item")) {
    
                    var itemName = props["item"].SValue;
    
                    // show a notice in reponse to button clicked
                    if (itemName.Equals("successbutton")) {
    
                        // the dispNotice() function is only present in API >= 2.1, so
                        //  fall-back to notice() function if running on older SP SDK API
                        if(dispNoticeEnabled)
                            pendant.dispNotice(Disposition.Positive, "Success", "It worked!");
                        else
                            pendant.notice("Success", "It worked!");
                    }
                    else if (itemName.Equals("noticebutton")) {
                        pendant.notice("A Notice","For your information.");
                    }
    
                }
    
            } catch (Exception ex) {
                // display error
                Console.WriteLine("Unable to process Clicked event :"+exceptionMessage(ex));
            }
        }
    
        void onJogPanelButtonClicked(PendantEvent e)
        {
            // jog panel buttin clicked, issue a user notice
            try {
                var id = e.Props["identifier"].SValue;
    
                pendant.notice("jog_panel_button_clicked","the_id_button_was_clicked",id);
    
            } catch (Exception ex) {
                // display error
                Console.WriteLine("Unable to process Jog Panel button click :"+exceptionMessage(ex));
            }
    
        }
    
    
        void onLayoutItemClicked(PendantEvent e)
        {
            try {
                var itemName = e.Props["item"].SValue;
    
                if (itemName.Equals("row1spacingup")) {
                    var spacing = pendant.property("layoutcontent","itemspacing").IValue;
                    pendant.setProperty("layoutcontent", "itemspacing", spacing+4);
                }
                else if (itemName.Equals("row1spacingdown")) {
                    var spacing = pendant.property("layoutcontent","itemspacing").IValue;
                    pendant.setProperty("layoutcontent", "itemspacing", spacing-4);
                }
            } catch (Exception ex) {
                // display error
                Console.WriteLine("Unable to process Layout tab event :"+exceptionMessage(ex));
            }
        }
    
    
        void onEventsItemClicked(PendantEvent e)
        {
            try {
                pendant.setProperty("eventtext1","text",e.ToString());
    
                var props = e.Props;
                if (props.ContainsKey("item")) {
    
                    var itemName = props["item"].SValue;
    
                    // if the popupquestion was Clicked, open a popupDialog question
                    if (itemName.Equals("popupquestion")) {
                        pendant.popupDialog("myeventpopup1", "a_popup_dialog",
                                            "popup_question",
                                            "popup_q_positive","popup_q_negative");
                    }
                }
    
            } catch (Exception ex) {
                // display error
                Console.WriteLine("Unable to process Clicked event :"+exceptionMessage(ex));
            }
        }
    
        void onNetworkSendClicked(PendantEvent e)
        {
            byte[] bytes = new byte[1024];  //data buffer for incoming data
            int bytesRec = 0;
            // get data & address to send TCP message
            var data = pendant.property("networkData","text").SValue+"\n";
            Encoding enc = Encoding.UTF8;
            IPAddress ipAddress = IPAddress.Parse(pendant.property("networkIPAddress","text").SValue);
            var port = int.Parse(pendant.property("networkPort","text").SValue);
    
            // create a port access to the outside
            //  (this would usually be done once during setup/init,
            //   but in this case the port is dymamic)
            var accessHandle = controller.requestNetworkAccess("LAN",port, "tcp");
            try {
                // open TCP socket
                IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName()); 
                IPAddress socketIpAddress = ipHostInfo.AddressList[0];
                IPEndPoint remoteEP = new IPEndPoint(ipAddress,port); 
                Socket socket = new Socket(socketIpAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socket.SendTimeout = 1500;
                try
                {
                    socket.Connect(remoteEP);
                    Console.WriteLine("Socket connected to {0}",  
                        socket.RemoteEndPoint);
                    //https://docs.microsoft.com/en-us/dotnet/framework/network-programming/synchronous-client-socket-example
                    var utf8Data = enc.GetBytes(data);
                    socket.Send(utf8Data);
                    bytesRec = socket.Receive(bytes);  
                    Console.WriteLine("Echoed test = {0}",  
                        Encoding.ASCII.GetString(bytes,0,bytesRec));
                    if(bytesRec != 0)
                        pendant.notice("Data Sent","The data was sent to "+ipAddress+":"+port,"");
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                } catch (ArgumentNullException ane) {  
                    Console.WriteLine("ArgumentNullException : {0}",ane);
                    pendant.setProperty("networkError","text",ane.ToString());
                } catch (SocketException se) {  
                    Console.WriteLine("SocketException : {0}",se);
                    pendant.setProperty("networkError","text",se.ToString());
                } catch (Exception err) {  
                    Console.WriteLine("Unexpected exception : {0}", err);
                    pendant.setProperty("networkError","text",err.ToString());
                }
            }catch (Exception erm) {  
                Console.WriteLine( erm.ToString());
                pendant.setProperty("networkError","text",erm.ToString());  
            }  
            pendant.setProperty("networkResponse","text",Encoding.ASCII.GetString(bytes,0,bytesRec));
            controller.removeNetworkAccess(accessHandle);
        }
    
    
        void onInsertInstructionControls(PendantEvent e)
        {
            try {
                String cmd = "";
                var props = e.Props;
                var itemName = props["item"].SValue;
    
                if (itemName.Equals("instructionSelect")) {
                    var index = props["index"].IValue;
                    if (index == 0)
                        pendant.setProperty("instructionText", "text", "CALL JOB:OR_RG_MOVE (1, 0, 40, \"WIDTH\")");
                    else if (index == 1)
                        pendant.setProperty("instructionText", "text", "GETS B000 $B000");
                }
                else if (itemName.Equals("insertInstruction")) {
    
                    // Insert cmd INFORM text into the current job at the current selected line
                    cmd = pendant.property("instructionText", "text").SValue;
    
                    String output = pendant.insertInstructionAtSelectedLine(cmd);
    
                    Console.WriteLine("Command Insertion result: " + output);
    
                    pendant.setProperty("instructionInsertResult", "text", "Result:" + output);
                }
    
    
            } catch (Exception ex) {
                // display error
                var error = exceptionMessage(ex);
                pendant.setProperty("instructionInsertResult","text",error);
                Console.WriteLine("Unable to handle instruction insertion:"+error);
            }
    
        }
    
        private bool init = false;
        private bool PingPendant() // ping pendant so it is kept alive
        {
            try
            {
                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return true;
            }
        }
        public void close()
        {
            run = false;
        }



        public static void Main(String[] args)
        {
            DemoExtension thisExtension = null;
            try
            {

                // launch
                try
                {
                    thisExtension = new DemoExtension();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Extension failed to start, aborting: " + exceptionMessage(e));
                    return;
                }

                try
                {
                    thisExtension.setup();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Extension failed in setup, aborting: " + exceptionMessage(e));
                    return;
                }

                // run 'forever' (or until API service shutsdown)
                try
                {
                    thisExtension.extension.run(thisExtension.PingPendant);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception occured:" + exceptionMessage(e));
                }

            }
            catch (Exception e)
            {

                Console.WriteLine("Exception: " + exceptionMessage(e));

            }
            finally
            {
                if (thisExtension != null)
                    thisExtension.close();
            }
        }

        protected Thread updThread;
        protected int updRate;
        protected double chartScale;
        protected double time;
        protected bool run = true;
        protected bool update = false;
        private bool dispNoticeEnabled = false;

        private Yaskawa.Ext.Extension extension;
        private static Yaskawa.Ext.Pendant pendant;
        private Yaskawa.Ext.Controller controller;
        private Yaskawa.Ext.Version apiVersion;

        protected String lang; // e.g. "en", "ja"
        protected String localeName; // e.g. "en", "ja", "ja_JP", "es_es", "es_mx"
    }
}