using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows;

namespace PD3_Monitor
{
    class bgtask
    {
        public Bluegiga.BGLib bglib;

        public List<Bluegiga.BLE.Events.GAP.ScanResponseEventArgs> lst_scan_result;
        public List<Hashtable> list_service_attributes;
        public string str_attribute_value;
        public byte[] streaming_data;
        public List<string> lst_raw_datas;
        public byte current_connection;
        public SerialPort serial_port;
        private int discover_service_id = 0, service_count = 0;
        public UInt16 att_handlesearch_start = 0;       // "start" handle holder during search
        public UInt16 att_handlesearch_end = 0;         // "end" handle holder during search
        public ushort error_code;
        public byte[] target_device_address;
        Bluegiga.BLE.Events.GAP.ScanResponseEventArgs current_scan_response;
        public TextBox textbox_receivedata;

        public enum App_State
        {
            STATE_STANDBY = 0,
            STATE_SCANNING,
            STATE_CONNECTING,
            STATE_CONNECTED,
            STATE_DISCONNECTED,
            STATE_FINDING_SERVICES,
            STATE_FINDING_ATTRIBUTES,
            STATE_WRITING_ATTRIBUTE,
            STATE_READING_ATTRIBUTE,
            STATE_STREAMING,
        }

        public App_State app_state;

        public bgtask(SerialPort sp, TextBox textbox)
        {
            this.serial_port = sp;
            this.textbox_receivedata = textbox;
            this.initialize();
        }

        private void initialize()
        {
            bglib = new Bluegiga.BGLib();
            bglib.BLEEventSystemBoot += new Bluegiga.BLE.Events.System.BootEventHandler(this.SystemBootEvent);
            bglib.BLEEventGAPScanResponse += new Bluegiga.BLE.Events.GAP.ScanResponseEventHandler(this.GAPScanResponseEvent);
            bglib.BLEEventConnectionStatus += new Bluegiga.BLE.Events.Connection.StatusEventHandler(this.ConnectionStatusEvent);
            bglib.BLEEventConnectionDisconnected += new Bluegiga.BLE.Events.Connection.DisconnectedEventHandler(this.ConnectionDisconnectedEvent);
            bglib.BLEEventATTClientGroupFound += new Bluegiga.BLE.Events.ATTClient.GroupFoundEventHandler(this.ATTClientGroupFoundEvent);
            bglib.BLEEventATTClientFindInformationFound += new Bluegiga.BLE.Events.ATTClient.FindInformationFoundEventHandler(this.ATTClientFindInformationFoundEvent);
            bglib.BLEEventATTClientProcedureCompleted += new Bluegiga.BLE.Events.ATTClient.ProcedureCompletedEventHandler(this.ATTClientProcedureCompletedEvent);
            bglib.BLEResponseATTClientReadByHandle += new Bluegiga.BLE.Responses.ATTClient.ReadByHandleEventHandler(this.ResponseAttributesReadByHandlerEvent);
            bglib.BLEEventATTClientAttributeValue += new Bluegiga.BLE.Events.ATTClient.AttributeValueEventHandler(this.ATTClientAttributeValueEvent);
            bglib.BLEResponseGAPEndProcedure += new Bluegiga.BLE.Responses.GAP.EndProcedureEventHandler(this.EndProcedureEvent);

            lst_scan_result = new List<Bluegiga.BLE.Events.GAP.ScanResponseEventArgs>();
            list_service_attributes = new List<Hashtable>();
            lst_raw_datas = new List<string>();
        }

        #region // dongle event/response functions

        public void SystemBootEvent(object sender, Bluegiga.BLE.Events.System.BootEventArgs e)
        {
            String log = String.Format("ble_evt_system_boot:" + Environment.NewLine + "\tmajor={0}, minor={1}, patch={2}, build={3}, ll_version={4}, protocol_version={5}, hw={6}" + Environment.NewLine,
                e.major,
                e.minor,
                e.patch,
                e.build,
                e.ll_version,
                e.protocol_version,
                e.hw
                );
        }

        public void GAPScanResponseEvent(object sender, Bluegiga.BLE.Events.GAP.ScanResponseEventArgs e)
        {
            String log = String.Format("ble_evt_gap_scan_response:" + Environment.NewLine + "\trssi={0}, packet_type={1}, bd_addr=[ {2}], address_type={3}, bond={4}, data=[ {5}]" + Environment.NewLine,
                (SByte)e.rssi,
                (SByte)e.packet_type,
                ByteArrayToHexString(e.sender),
                (SByte)e.address_type,
                (SByte)e.bond,
                ByteArrayToHexString(e.data)
                );
            SetTextBox(log);
            bool duplicated = false;
            int i = 0;
            foreach (Bluegiga.BLE.Events.GAP.ScanResponseEventArgs spea in lst_scan_result)
            {
                if (spea.sender.SequenceEqual(e.sender))
                {
                    duplicated = true;
                    lst_scan_result[i] = e;
                    break;
                }
                i++;
            }
            if (!duplicated)
            {
                lst_scan_result.Add(e);
            }

            if (app_state == App_State.STATE_CONNECTING)
            {
                if (e.sender.SequenceEqual(target_device_address))
                {
                    current_scan_response = e;
                    this.Stop_Scanning();
                }
            }
        }

        private void EndProcedureEvent(object sender, Bluegiga.BLE.Responses.GAP.EndProcedureEventArgs e)
        {
            String log = String.Format("ble_evt_gap_end_procedure:" + Environment.NewLine + "\tresult={0}" + Environment.NewLine,
                e.result
                );
            SetTextBox(log);
            if (app_state == App_State.STATE_CONNECTING)
            {
                target_device_address = null;
                Connect_Device(current_scan_response.sender, current_scan_response.address_type, 0x20, 0x30, 0x100, 0);
            }
        }
        // the "connection_status" event occurs when a new connection is established
        public void ConnectionStatusEvent(object sender, Bluegiga.BLE.Events.Connection.StatusEventArgs e)
        {
            String log = String.Format("ble_evt_connection_status: connection={0}, flags={1}, address=[ {2}], address_type={3}, conn_interval={4}, timeout={5}, latency={6}, bonding={7}" + Environment.NewLine,
                e.connection,
                e.flags,
                ByteArrayToHexString(e.address),
                e.address_type,
                e.conn_interval,
                e.timeout,
                e.latency,
                e.bonding
                );
            SetTextBox(log);
            if ((e.flags & 0x05) == 0x05)
            {
                this.current_connection = e.connection;
                app_state = App_State.STATE_CONNECTED;
                // connected, now perform service discovery
                //Byte[] cmd = bglib.BLECommandATTClientReadByGroupType(e.connection, 0x0001, 0xFFFF, new Byte[] { 0x00, 0x28 }); // "service" UUID is 0x2800 ( primary service)
                //bglib.SendCommand(serial_port, cmd);
                //app_state = App_State.STATE_FINDING_SERVICES;
            }
        }

        public void ConnectionDisconnectedEvent(object sender, Bluegiga.BLE.Events.Connection.DisconnectedEventArgs e)
        {
            String log = String.Format("ble_evt_connection_disconnected: connection={0}, reason={1}, " + Environment.NewLine,
                e.connection,
                e.reason
                );
            SetTextBox(log);
            this.current_connection = e.connection;
            app_state = App_State.STATE_DISCONNECTED;
        }

        public void ATTClientGroupFoundEvent(object sender, Bluegiga.BLE.Events.ATTClient.GroupFoundEventArgs e)
        {
            String log = String.Format("ble_evt_attclient_group_found: connection={0}, start={1}, end={2}, uuid=[ {3}]" + Environment.NewLine,
                e.connection,
                e.start,
                e.end,
                ByteArrayToHexString(e.uuid)
                );
            SetTextBox(log);
            Hashtable hash_table = new Hashtable();
            hash_table.Add("uuid", e.uuid);
            hash_table.Add("start_id", e.start);
            hash_table.Add("end_id", e.end);
            hash_table.Add("type", "service");
            service_count++;
            this.list_service_attributes.Add(hash_table);
        }

        public void ATTClientFindInformationFoundEvent(object sender, Bluegiga.BLE.Events.ATTClient.FindInformationFoundEventArgs e)
        {
            String log = String.Format("ble_evt_attclient_find_information_found: connection={0}, chrhandle={1}, uuid=[ {2}]" + Environment.NewLine,
                e.connection,
                e.chrhandle,
                ByteArrayToHexString(e.uuid)
                );
            SetTextBox(log);
            Hashtable hash_table = new Hashtable();
            hash_table.Add("uuid", e.uuid);
            hash_table.Add("chrhandle", e.chrhandle);
            hash_table.Add("service_id", discover_service_id - 1);
            hash_table.Add("type", "attribute");
            list_service_attributes.Add(hash_table);
        }

        public void ATTClientProcedureCompletedEvent(object sender, Bluegiga.BLE.Events.ATTClient.ProcedureCompletedEventArgs e)
        {
            String log = String.Format("ble_evt_attclient_procedure_completed: connection={0}, result={1}, chrhandle={2}" + Environment.NewLine,
                e.connection,
                e.result,
                e.chrhandle
                );
            SetTextBox(log);
            error_code = e.result;

            // check if we just finished searching for services
            if (app_state == App_State.STATE_FINDING_SERVICES)
            {
                if (list_service_attributes.Count > 0)
                {
                    int idx = service_count;
                    if (idx > 0)
                    {
                        att_handlesearch_start = (ushort)((Hashtable)(list_service_attributes[discover_service_id]))["start_id"];
                        att_handlesearch_end = (ushort)((Hashtable)(list_service_attributes[discover_service_id]))["end_id"];

                        Byte[] cmd = bglib.BLECommandATTClientFindInformation(e.connection, att_handlesearch_start, att_handlesearch_end);
                        bglib.SendCommand(serial_port, cmd);
                        discover_service_id++;
                        if (discover_service_id == service_count)
                        {
                            app_state = App_State.STATE_FINDING_ATTRIBUTES;
                        }
                    }
                }
            }// check if we just finished searching for attributes within the heart rate service
            else if (app_state == App_State.STATE_FINDING_ATTRIBUTES)
            {
                app_state = App_State.STATE_STANDBY;
            }
            else if (app_state == App_State.STATE_WRITING_ATTRIBUTE)
            {
                if (e.result == 0)
                {
                    app_state = App_State.STATE_STANDBY;
                }
            }

            if (e.chrhandle == 41)
            {
                // start streaming 785
                Byte[] cmd2 = bglib.BLECommandATTClientAttributeWrite(current_connection, 31, new Byte[] { 0x01 });
                // DEBUG: display bytes written
                bglib.SendCommand(serial_port, cmd2);
                app_state = App_State.STATE_STREAMING;
            }


        }

        public void ATTClientAttributeValueEvent(object sender, Bluegiga.BLE.Events.ATTClient.AttributeValueEventArgs e)
        {
            String log = String.Format("ble_evt_attclient_attribute_value: connection={0}, atthandle={1}, type={2}, value=[ {3}]" + Environment.NewLine,
                e.connection,
                e.atthandle,
                e.type,
                ByteArrayToHexString(e.value)
                );
            if (app_state == App_State.STATE_READING_ATTRIBUTE)
            {
                SetTextBox(log);
                str_attribute_value = ByteArrayToHexString(e.value).Replace(" ", "");
            }
            else if (app_state == App_State.STATE_STREAMING)
            {
                streaming_data = e.value;
                string strTemp = ByteArrayToHexString(e.value).Replace(" ", "");

                SetTextBox(strTemp + Environment.NewLine);
            }

        }

        public void ResponseAttributesReadByHandlerEvent(object sender, Bluegiga.BLE.Responses.ATTClient.ReadByHandleEventArgs e)
        {
            String log = String.Format("ble_evt_attribute_value_read_by_handler: connection={0}, result={1}" + Environment.NewLine,
                e.connection,
                e.result
                );
            SetTextBox(log);
            error_code = e.result;
        }
        #endregion

        #region // dongle operation functions
        public void Scanning_Devices()
        {
            app_state = App_State.STATE_SCANNING;
            lst_scan_result = new List<Bluegiga.BLE.Events.GAP.ScanResponseEventArgs>();
            bglib.SendCommand(serial_port, bglib.BLECommandGAPDiscover(1));
        }

        public void Stop_Scanning()
        {
            bglib.SendCommand(serial_port, bglib.BLECommandGAPEndProcedure());
        }

        public void Connect_Device(byte[] address, byte address_type, ushort conn_interval_min, ushort conn_interval_max, ushort timeout, ushort latency)
        {
            // 0x20, 0x30, 0x100, 0 => 125ms interval, 125ms window, active scanning
            list_service_attributes = new List<Hashtable>();
            app_state = App_State.STATE_CONNECTING;
            Byte[] cmd = bglib.BLECommandGAPConnectDirect(address, address_type, conn_interval_min, conn_interval_max, timeout, latency);
            bglib.SendCommand(serial_port, cmd);
        }

        public void Connect_Device()
        {
            app_state = App_State.STATE_CONNECTING;
        }

        public void Disconnect_Device()
        {
            Byte[] cmd = bglib.BLECommandConnectionDisconnect(current_connection);
            bglib.SendCommand(serial_port, cmd);
        }

        public void Read_Atrribute(ushort channelid, Byte connection)
        {
            app_state = App_State.STATE_READING_ATTRIBUTE;
            Byte[] cmd = bglib.BLECommandATTClientReadByHandle(connection, channelid);
            bglib.SendCommand(serial_port, cmd);
        }

        public void Write_Atrribute(ushort channelid, Byte connection, byte[] value)
        {
            app_state = App_State.STATE_WRITING_ATTRIBUTE;
            Byte[] cmd = bglib.BLECommandATTClientAttributeWrite(connection, channelid, value);
            bglib.SendCommand(serial_port, cmd);
        }
        #endregion

        public string getHexstringfromArray(byte[] bytes, string spe)
        {
            StringBuilder hex = new StringBuilder(bytes.Length * 2);
            for (int i = bytes.Length - 1; i >= 0; i--)
            {
                hex.AppendFormat("{0:X2}" + spe, bytes[i]);
            }

            return hex.ToString();
        }

        public string ByteArrayToHexString(Byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2} ", b);
            return hex.ToString();
        }

        private string getAdress(byte[] device_address)
        {
            StringBuilder hex = new StringBuilder(device_address.Length * 2);
            foreach (byte b in device_address)
                hex.AppendFormat("{0:x2}:", b);

            string str_address = hex.ToString();
            return str_address.Substring(0, str_address.Length - 1);
        }

        private void SetTextBox(string text)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(new Action<string>(SetTextBox), new object[] { text });
                return;
            }
            textbox_receivedata.AppendText(text);
        }
    }
}
