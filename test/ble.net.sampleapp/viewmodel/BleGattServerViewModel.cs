﻿// Copyright Malachi Griffie
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Acr.UserDialogs;
using ble.net.sampleapp.util;
using nexus.core;
using nexus.core.logging;
using nexus.protocols.ble;
using nexus.protocols.ble.connection;

namespace ble.net.sampleapp.viewmodel
{
   public class BleGattServerViewModel : BaseViewModel
   {
      private readonly IBluetoothLowEnergyAdapter m_bleAdapter;
      private readonly IUserDialogs m_dialogManager;
      private String m_connectionState;
      private IBleGattServer m_gattServer;
      private Boolean m_isBusy;
      private BlePeripheralViewModel m_peripheral;

      public BleGattServerViewModel( IUserDialogs dialogsManager, IBluetoothLowEnergyAdapter bleAdapter )
      {
         m_bleAdapter = bleAdapter;
         m_dialogManager = dialogsManager;
         m_connectionState = ConnectionState.Disconnected.ToString();
         Services = new ObservableCollection<BleGattServiceViewModel>();
      }

      public String Connection
      {
         get { return m_connectionState; }
         private set { Set( ref m_connectionState, value ); }
      }

      public Boolean IsBusy
      {
         get { return m_isBusy; }
         protected set { Set( ref m_isBusy, value ); }
      }

      public String Manufacturer => m_peripheral?.Manufacturer;

      public String Name => m_peripheral?.Name;

      public String PageTitle => Name + " (" + Manufacturer + ")";

      public ObservableCollection<BleGattServiceViewModel> Services { get; }

      public void CloseConnection()
      {
         Log.Trace( "{0}. Closing connection to GATT Server. state={1:g}", GetType().Name, m_gattServer?.State );
         m_gattServer?.Dispose();
         Services.Clear();
         IsBusy = false;
      }

      public override async void OnAppearing()
      {
         // if we're busy or have a valid connection, then no-op
         if(IsBusy || (m_gattServer != null && m_gattServer.State != ConnectionState.Disconnected))
         {
            Log.Debug( "OnAppearing. state={0} isbusy={1}", m_gattServer?.State, IsBusy );
            return;
         }

         CloseConnection();
         IsBusy = true;

         Log.Debug( "Connecting to device. id={0}", m_peripheral.Id );
         var connection =
            await
               m_bleAdapter.ConnectToDevice(
                  device: m_peripheral.Model,
                  timeout: TimeSpan.FromSeconds( 5 ),
                  progress: progress => { Connection = progress.ToString(); } );
         if(connection.IsSuccessful())
         {
            m_gattServer = connection.GattServer;
            Connection = "Reading Services";
            Log.Debug( "Connected to device. id={0} status={1}", m_peripheral.Id, m_gattServer.State );

            m_gattServer.Subscribe(
               c =>
               {
                  if(c == ConnectionState.Disconnected)
                  {
                     m_dialogManager.Toast( "Device disconnected" );
                     CloseConnection();
                  }
                  Connection = c.ToString();
               } );

            // small possibility the device could disconnect between connecting and getting services and throw somewhere along here
            var services = (await m_gattServer.ListAllServices()).ToList();
            foreach(var serviceId in services)
            {
               if(Services.Any( viewModel => viewModel.Guid.Equals( serviceId ) ))
               {
                  continue;
               }
               Services.Add( new BleGattServiceViewModel( serviceId, m_gattServer, m_dialogManager ) );
            }
            if(Services.Count == 0)
            {
               m_dialogManager.Toast( "No services found" );
            }
            Connection = m_gattServer.State.ToString();
         }
         else
         {
            var errorMsg =
               "Error connecting to device: {0}".F(
                  connection.ConnectionResult == ConnectionResult.ConnectionAttemptCancelled
                     ? "Timeout"
                     : connection.ConnectionResult.ToString() );
            Log.Info( errorMsg );
            m_dialogManager.Toast( errorMsg, TimeSpan.FromSeconds( 5 ) );
         }
         IsBusy = false;
      }

      public void Update( BlePeripheralViewModel peripheral )
      {
         if(m_peripheral != null && !m_peripheral.Model.Equals( peripheral.Model ))
         {
            CloseConnection();
         }
         m_peripheral = peripheral;
      }
   }
}