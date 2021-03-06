﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Swarmops.Basic.Types.Financial;
using Swarmops.Common.Enums;
using Swarmops.Common.Interfaces;
using Swarmops.Database;
using Swarmops.Logic.Structure;

namespace Swarmops.Logic.Financial
{
    public class VatReport: BasicVatReport
    {
        #region Creation and Construction

        private VatReport(BasicVatReport basic): base (basic)
        {
            // private ctor prevents random instantiation
        }

        public static VatReport FromBasic(BasicVatReport basic)
        {
            // Interface to private ctor
            return new VatReport(basic);
        }

        public static VatReport FromIdentity(int vatReportId)
        {
            return FromBasic(SwarmDb.GetDatabaseForReading().GetVatReport(vatReportId));
        }

        protected static VatReport FromIdentityAggressive(int vatReportId)
        {
            // "Open For Writing" is intentional - it bypasses the lag of replication-to-readonly
            // instances of large-deployment databases and reads at the write source

            return FromBasic(SwarmDb.GetDatabaseForWriting().GetVatReport(vatReportId));
        }

        public static VatReport FromGuid(string guid)
        {
            int vatReportId = SwarmDb.GetDatabaseForReading().GetVatReportIdFromGuid(guid);

            if (vatReportId == 0)
            {
                throw new ArgumentException("No such report");
            }

            return FromIdentity(vatReportId);
        }

        private static VatReport CreateDbRecord (Organization organization, int year, int startMonth, int monthCount)
        {
            System.Guid guid = System.Guid.NewGuid();

            int vatReportId = SwarmDb.GetDatabaseForWriting()
                .CreateVatReport(organization.Identity, guid.ToString(), year*100 + startMonth, monthCount);
            return FromIdentityAggressive(vatReportId);
        }

        public static VatReport Create(Organization organization, int year, int startMonth, int monthCount)
        {
            VatReport newReport = CreateDbRecord(organization, year, startMonth, monthCount);
            DateTime endDate = new DateTime(year, startMonth, 1).AddMonths(monthCount);

            FinancialAccount vatInbound = organization.FinancialAccounts.AssetsVatInboundUnreported;
            FinancialAccount vatOutbound = organization.FinancialAccounts.DebtsVatOutboundUnreported;
            FinancialAccount sales = organization.FinancialAccounts.IncomeSales;
            FinancialAccounts salesTree = sales.ThisAndBelow();

            FinancialAccountRows inboundRows = RowsNotInVatReport(vatInbound, endDate);
            FinancialAccountRows outboundRows = RowsNotInVatReport(vatOutbound, endDate);
            FinancialAccountRows turnoverRows = RowsNotInVatReport(salesTree, endDate);

            Dictionary<int, bool> transactionsIncludedLookup = new Dictionary<int, bool>();

            newReport.AddVatReportItemsFromAccountRows(inboundRows, transactionsIncludedLookup);
            newReport.AddVatReportItemsFromAccountRows(outboundRows, transactionsIncludedLookup);
            newReport.AddVatReportItemsFromAccountRows(turnoverRows, transactionsIncludedLookup);

            newReport.Release();

            // Create financial TX that moves this VAT from unreported to reported

            Int64 differenceCents = newReport.VatInboundCents - newReport.VatOutboundCents;

            if (differenceCents != 0 && newReport.VatInboundCents > 0)
            {
                // if there's anything to report

                FinancialTransaction vatReportTransaction = FinancialTransaction.Create(organization, endDate.AddDays(4).AddHours(9),
                    newReport.Description);

                if (newReport.VatInboundCents > 0)
                {
                    vatReportTransaction.AddRow(organization.FinancialAccounts.AssetsVatInboundUnreported,
                        -newReport.VatInboundCents, null);
                }
                if (newReport.VatOutboundCents > 0)
                {
                    vatReportTransaction.AddRow(organization.FinancialAccounts.DebtsVatOutboundUnreported,
                        newReport.VatOutboundCents, null);
                        // not negative, because our number is sign-different from the bookkeeping's
                }

                if (differenceCents < 0) // outbound > inbound
                {
                    vatReportTransaction.AddRow(organization.FinancialAccounts.DebtsVatOutboundReported,
                        differenceCents, null); // debt, so negative as in our variable
                }
                else // inbound > outbound
                {
                    vatReportTransaction.AddRow(organization.FinancialAccounts.AssetsVatInboundReported,
                        differenceCents, null); // asset, so positive as in our variable
                }

                vatReportTransaction.Dependency = newReport;
                newReport.OpenTransaction = vatReportTransaction;
            }
            else
            {
                newReport.Open = false; // nothing to close, no tx created
            }

            return newReport;
        }

        private void AddVatReportItemsFromAccountRows(FinancialAccountRows rows,
            Dictionary<int, bool> transactionsIncludedLookup)
        {
            if (rows.Count == 0)
            {
                return;
            }

            Organization organization = rows[0].Transaction.Organization; // there is always a rows[0] because check above
            int vatInboundAccountId = organization.FinancialAccounts.AssetsVatInboundUnreported.Identity;
            int vatOutboundAccountId = organization.FinancialAccounts.DebtsVatOutboundUnreported.Identity;

            Dictionary<int, bool> turnoverAccountLookup = new Dictionary<int, bool>();

            FinancialAccounts turnoverAccounts = FinancialAccounts.ForOrganization(organization,
                FinancialAccountType.Income);

            foreach (FinancialAccount turnoverAccount in turnoverAccounts)
            {
                turnoverAccountLookup[turnoverAccount.Identity] = true;
            }

            foreach (FinancialAccountRow accountRow in rows)
            {
                FinancialTransaction tx = accountRow.Transaction;

                if (tx.Dependency is VatReport)
                {
                    continue; // Never include previous VAT reports in new VAT reports
                }

                if (!transactionsIncludedLookup.ContainsKey(tx.Identity))
                {
                    Int64 vatInbound = 0;
                    Int64 vatOutbound = 0;
                    Int64 turnOver = 0;

                    transactionsIncludedLookup[accountRow.FinancialTransactionId] = true;

                    FinancialTransactionRows txRows = accountRow.Transaction.Rows;

                    foreach (FinancialTransactionRow txRow in txRows)
                    {
                        if (txRow.FinancialAccountId == vatInboundAccountId)
                        {
                            vatInbound += txRow.AmountCents;
                        }
                        else if (txRow.FinancialAccountId == vatOutboundAccountId)
                        {
                            vatOutbound += -txRow.AmountCents;  // this is a negative, so converting to positive
                        }
                        else if (turnoverAccountLookup.ContainsKey(txRow.FinancialAccountId))
                        {
                            turnOver -= txRow.AmountCents;  // turnover accounts are sign reversed, so convert to positive
                        }
                    }

                    // Add new row to the VAT report

                    AddItem(tx, turnOver, vatInbound, vatOutbound);
                }
            }
        }

        #endregion

        public void AddItem(FinancialTransaction transaction, Int64 turnoverCents,
            Int64 vatInboundCents, Int64 vatOutboundCents)
        {
            if (!UnderConstruction)
            {
                throw new InvalidOperationException("Cannot add items once the report is released");
            }

            VatReportItem.Create(this, transaction, turnoverCents, vatInboundCents, vatOutboundCents);
        }

        public void Release()
        {
            // if we're not under construction, throw exception

            if (!this.UnderConstruction)
            {
                throw new InvalidOperationException("VAT report is already released");
            }

            // Releasing calculates sums of turnover, VAT inbound, outbound across items into report record

            SwarmDb.GetDatabaseForWriting().SetVatReportReleased(this.Identity);
            
            base.UnderConstruction = false;
            base.TurnoverCents = Items.TurnoverCents;
            base.VatInboundCents = Items.VatInboundCents;
            base.VatOutboundCents = Items.VatOutboundCents;
        }

        public VatReportItems Items
        {
            get { return VatReportItems.ForReport(this); }
        }


        private static FinancialAccountRows RowsNotInVatReport(FinancialAccounts accounts, DateTime endTime)
        {
            FinancialAccountRows result = new FinancialAccountRows();

            result.AddRange(accounts.SelectMany(account => RowsNotInVatReport(account, endTime)));

            return result;
        }


        private static FinancialAccountRows RowsNotInVatReport(FinancialAccount account, DateTime endDateTime)
        {
            Organization organization = account.Organization;
            SwarmDb dbRead = SwarmDb.GetDatabaseForReading();

            FinancialAccountRows rowsIntermediate =
                FinancialAccountRows.FromArray(
                    SwarmDb.GetDatabaseForReading().GetAccountRowsNotInVatReport(account.Identity, endDateTime));

            FinancialAccountRows rowsFinal = new FinancialAccountRows();
            foreach (FinancialAccountRow row in rowsIntermediate)
            {
                // Check if this row _closes_ an _existing_ VAT report, in which case it should _not_ be included

                int vatReportOpenId = dbRead.GetVatReportIdFromCloseTransaction(row.FinancialTransactionId);
                int vatReportCloseId = dbRead.GetVatReportIdFromOpenTransaction(row.FinancialTransactionId);
                if (vatReportOpenId == 0 && vatReportCloseId == 0)
                {
                    // This particular transaction doesn't close an existing VAT report
                    rowsFinal.Add(row);
                }
            }

            return rowsFinal;
        }

        public string Description
        {
            get
            {
                switch (MonthCount)
                {
                    case 1:
                        return String.Format(Resources.Logic_Financial_VatReport.Description_SingleMonth,
                            new DateTime(YearMonthStart/100, YearMonthStart%100, 2));
                    case 12:
                        return String.Format(Resources.Logic_Financial_VatReport.Description_FullYear,
                            YearMonthStart/100);
                    default:
                        DateTime start = new DateTime(YearMonthStart/100, YearMonthStart%100, 2);

                        return String.Format(Resources.Logic_Financial_VatReport.Description_Months,
                            start, start.AddMonths(MonthCount - 1));
                }
            }
        }

        public string DescriptionShort
        {
            get
            {
                switch (MonthCount)
                {
                    case 1:
                        return String.Format("{0:MMM yyyy}",
                            new DateTime(YearMonthStart / 100, YearMonthStart % 100, 2));
                    case 12:
                        return String.Format("{0}",
                            YearMonthStart / 100);
                    default:
                        DateTime start = new DateTime(YearMonthStart / 100, YearMonthStart % 100, 2);

                        return String.Format("{0:MMM}-{1:MMM yyyy}",
                            start, start.AddMonths(MonthCount - 1));
                }
            }
        }

        private new int OpenTransactionId
        {
            get
            {
                return base.OpenTransactionId;
            }
            set
            {
                SwarmDb.GetDatabaseForWriting().SetVatReportOpenTransaction(this.Identity, value);
                base.OpenTransactionId = value;
            }
        }

        public FinancialTransaction OpenTransaction
        {
            get
            {
                if (base.OpenTransactionId == 0) return null;
                return FinancialTransaction.FromIdentity(OpenTransactionId);
            }
            set { this.OpenTransactionId = value.Identity; }
        }

        private new int CloseTransactionId
        {
            get { return base.CloseTransactionId; }
            set
            {
                SwarmDb.GetDatabaseForWriting().SetVatReportCloseTransaction(this.Identity, value);
                base.CloseTransactionId = value;
            }
        }

        public FinancialTransaction CloseTransaction
        {
            get
            {
                if (base.CloseTransactionId == 0) return null;
                return FinancialTransaction.FromIdentity(CloseTransactionId);
            }
            set
            {
                if (value != null)
                {
                    this.CloseTransactionId = value.Identity;
                    value.Dependency = this;
                    this.Open = false;
                }
                else
                {
                    throw new ArgumentNullException(); // is this a valid case?
                }
            }
        }

        public new bool Open
        {
            get { return base.Open; }
            set
            {
                SwarmDb.GetDatabaseForWriting().SetVatReportOpen(this.Identity, value);
                base.Open = value;
            }
        }
    }

}
