﻿// <copyright>
// Copyright 2019 by Kingdom First Solutions
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.ComponentModel;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Web.UI;

using rocks.kfs.Intacct;

namespace RockWeb.Plugins.rocks_kfs.Intacct
{
    #region Block Attributes

    [DisplayName( "Intacct Batch to Journal" )]
    [Category( "KFS > Intacct" )]
    [Description( "Block used to create Journal Entries in Intacct from a Rock Financial Batch." )]

    #endregion

    #region Block Settings

    [TextField( "Journal Id", "The Intacct Symbol of the Journal that the Entry should be posted to. For example: GJ", true, "", "", 0 )]
    [TextField( "Button Text", "The text to use in the Export Button.", false, "Export to Intacct", "", 1 )]
    [BooleanField( "Close Batch", "Flag indicating if the Financial Batch be closed in Rock when successfully posted to Intacct.", true, "", 2 )]
    [BooleanField( "Log Response", "Flag indicating if the Intacct Response should be logged to the Batch Audit Log", true, "", 3 )]
    [EncryptedTextField( "Sender Id", "The permanent Web Services sender Id.", true, "", "Configuration", 0 )]
    [EncryptedTextField( "Sender Password", "The permanent Web Services sender password.", true, "", "Configuration", 1 )]
    [EncryptedTextField( "Company Id", "The Intacct Company Id. This is the same information you use when you log into the Sage Intacct UI.", true, "", "Configuration", 2 )]
    [EncryptedTextField( "User Id", "The Intacct User Id. This is the same information you use when you log into the Sage Intacct UI.", true, "", "Configuration", 3 )]
    [EncryptedTextField( "User Password", "The Intacct User Password. This is the same information you use when you log into the Sage Intacct UI.", true, "", "Configuration", 4 )]
    [EncryptedTextField( "Location Id", "The optional Intacct Location Id. Add a location ID to log into a multi-entity shared company. Entities are typically different locations of a single company.", false, "", "Configuration", 5 )]
    [LavaField( "Journal Memo Lava", "Lava for the journal memo per line. Default: Batch.Id: Batch.Name", true, "{{ Batch.Id }}: {{ Batch.Name }}" )]
    [BooleanField( "Enable Debug", "Outputs the object graph to help create your Lava syntax. (Debug data will show after clicking export.)", false )]

    #endregion

    public partial class BatchToJournal : RockBlock
    {
        private int _batchId = 0;
        private FinancialBatch _financialBatch = null;

        #region Control Methods

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            _batchId = PageParameter( "batchId" ).AsInteger();
        }

        /// <summary>
        /// Handles the BlockUpdated event of the Block control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Block_BlockUpdated( object sender, EventArgs e )
        {
            ShowDetail();
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            ShowDetail();
        }

        #endregion Control Methods

        #region Methods

        protected void ShowDetail()
        {
            var rockContext = new RockContext();
            var isExported = false;
            var debugEnabled = GetAttributeValue( "EnableDebug" ).AsBoolean();

            _financialBatch = new FinancialBatchService( rockContext ).Get( _batchId );
            DateTime? dateExported = null;

            decimal variance = 0;

            if ( _financialBatch != null )
            {
                var financialTransactionDetailService = new FinancialTransactionDetailService( rockContext );
                var qryTransactionDetails = financialTransactionDetailService.Queryable().Where( a => a.Transaction.BatchId == _financialBatch.Id );
                decimal txnTotal = qryTransactionDetails.Select( a => ( decimal? ) a.Amount ).Sum() ?? 0;
                variance = txnTotal - _financialBatch.ControlAmount;

                _financialBatch.LoadAttributes();

                dateExported = ( DateTime? ) _financialBatch.AttributeValues["rocks.kfs.Intacct.DateExported"].ValueAsType;

                if ( dateExported != null && dateExported > DateTime.MinValue )
                {
                    isExported = true;
                }

                if ( debugEnabled )
                {
                    var debugLava = Session["IntacctDebugLava"].ToStringSafe();
                    if ( !string.IsNullOrWhiteSpace( debugLava ) )
                    {
                        lDebug.Visible = true;
                        lDebug.Text += debugLava;
                        Session["IntacctDebugLava"] = string.Empty;
                    }
                }
            }

            if ( ValidSettings() && !isExported )
            {
                btnExportToIntacct.Text = GetAttributeValue( "ButtonText" );
                btnExportToIntacct.Visible = true;
                if ( variance == 0 )
                {
                    btnExportToIntacct.Enabled = true;
                }
                else
                {
                    btnExportToIntacct.Enabled = false;
                }
            }
            else if ( isExported )
            {
                litDateExported.Text = string.Format( "<div class=\"small\">Exported: {0}</div>", dateExported.ToRelativeDateString() );
                litDateExported.Visible = true;

                if ( UserCanEdit )
                {
                    btnRemoveDate.Visible = true;
                }
            }
        }

        protected void btnExportToIntacct_Click( object sender, EventArgs e )
        {
            if ( _financialBatch != null )
            {
                //
                // Get Intacct Auth
                //

                var intacctAuth = new IntacctAuth()
                {
                    SenderId = Encryption.DecryptString( GetAttributeValue( "SenderId" ) ),
                    SenderPassword = Encryption.DecryptString( GetAttributeValue( "SenderPassword" ) ),
                    CompanyId = Encryption.DecryptString( GetAttributeValue( "CompanyId" ) ),
                    UserId = Encryption.DecryptString( GetAttributeValue( "UserId" ) ),
                    UserPassword = Encryption.DecryptString( GetAttributeValue( "UserPassword" ) ),
                    LocationId = Encryption.DecryptString( GetAttributeValue( "LocationId" ) )
                };

                //
                // Create Intacct Journal XML and Post to Intacct
                //

                var journal = new IntacctJournal();
                var endpoint = new IntacctEndpoint();
                var debugLava = GetAttributeValue( "EnableDebug" );

                var postXml = journal.CreateJournalEntryXML( intacctAuth, _financialBatch.Id, GetAttributeValue( "JournalId" ), ref debugLava, GetAttributeValue( "JournalMemoLava" ) );
                var resultXml = endpoint.PostToIntacct( postXml );
                var success = endpoint.ParseEndpointResponse( resultXml, _financialBatch.Id, GetAttributeValue( "LogResponse" ).AsBoolean() );

                if ( success )
                {
                    var rockContext = new RockContext();
                    var financialBatch = new FinancialBatchService( rockContext ).Get( _batchId );
                    var changes = new History.HistoryChangeList();

                    Session["IntacctDebugLava"] = debugLava;

                    //
                    // Close Batch if we're supposed to
                    //
                    if ( GetAttributeValue( "CloseBatch" ).AsBoolean() )
                    {
                        History.EvaluateChange( changes, "Status", financialBatch.Status, BatchStatus.Closed );
                        financialBatch.Status = BatchStatus.Closed;
                    }

                    //
                    // Set Date Exported
                    //
                    financialBatch.LoadAttributes();
                    var oldDate = financialBatch.GetAttributeValue( "rocks.kfs.Intacct.DateExported" );
                    var newDate = RockDateTime.Now;
                    History.EvaluateChange( changes, "Date Exported", oldDate, newDate.ToString() );

                    //
                    // Save the changes
                    //
                    rockContext.WrapTransaction( () =>
                    {
                        if ( changes.Any() )
                        {
                            HistoryService.SaveChanges(
                                rockContext,
                                typeof( FinancialBatch ),
                                Rock.SystemGuid.Category.HISTORY_FINANCIAL_BATCH.AsGuid(),
                                financialBatch.Id,
                                changes );
                        }
                    } );

                    financialBatch.SetAttributeValue( "rocks.kfs.Intacct.DateExported", newDate );
                    financialBatch.SaveAttributeValue( "rocks.kfs.Intacct.DateExported", rockContext );
                }
            }

            Response.Redirect( Request.RawUrl );
        }

        protected void btnRemoveDateExported_Click( object sender, EventArgs e )
        {
            if ( _financialBatch != null )
            {
                var rockContext = new RockContext();
                var financialBatch = new FinancialBatchService( rockContext ).Get( _batchId );
                var changes = new History.HistoryChangeList();

                //
                // Open Batch is we Closed it
                //
                if ( GetAttributeValue( "CloseBatch" ).AsBoolean() )
                {
                    History.EvaluateChange( changes, "Status", financialBatch.Status, BatchStatus.Open );
                    financialBatch.Status = BatchStatus.Open;
                }

                //
                // Remove Date Exported
                //
                financialBatch.LoadAttributes();
                var oldDate = financialBatch.GetAttributeValue( "rocks.kfs.Intacct.DateExported" ).AsDateTime().ToString();
                var newDate = string.Empty;
                History.EvaluateChange( changes, "Date Exported", oldDate, newDate );

                //
                // Save the changes
                //
                rockContext.WrapTransaction( () =>
                {
                    if ( changes.Any() )
                    {
                        HistoryService.SaveChanges(
                            rockContext,
                            typeof( FinancialBatch ),
                            Rock.SystemGuid.Category.HISTORY_FINANCIAL_BATCH.AsGuid(),
                            financialBatch.Id,
                            changes );
                    }
                } );

                financialBatch.SetAttributeValue( "rocks.kfs.Intacct.DateExported", newDate );
                financialBatch.SaveAttributeValue( "rocks.kfs.Intacct.DateExported", rockContext );
            }

            Response.Redirect( Request.RawUrl );
        }

        protected bool ValidSettings()
        {
            var settings = false;

            if (
                _batchId > 0 &&
                (
                !string.IsNullOrWhiteSpace( Encryption.DecryptString( GetAttributeValue( "SenderId" ) ) ) &&
                !string.IsNullOrWhiteSpace( Encryption.DecryptString( GetAttributeValue( "SenderPassword" ) ) ) &&
                !string.IsNullOrWhiteSpace( Encryption.DecryptString( GetAttributeValue( "CompanyId" ) ) ) &&
                !string.IsNullOrWhiteSpace( Encryption.DecryptString( GetAttributeValue( "UserId" ) ) ) &&
                !string.IsNullOrWhiteSpace( Encryption.DecryptString( GetAttributeValue( "UserPassword" ) ) ) &&
                !string.IsNullOrWhiteSpace( GetAttributeValue( "JournalId" ) )
                )
             )
            {
                settings = true;
            }

            return settings;
        }

        #endregion Methods
    }
}
