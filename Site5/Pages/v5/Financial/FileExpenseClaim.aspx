﻿<%@ Page Title="" Language="C#" MasterPageFile="~/Master-v5.master" AutoEventWireup="true" CodeFile="FileExpenseClaim.aspx.cs" Inherits="Swarmops.Frontend.Pages.v5.Financial.FileExpenseClaim" %>
<%@ Register src="~/Controls/v5/UI/ExternalScripts.ascx" tagname="ExternalScripts" tagprefix="Swarmops5" %>

<asp:Content ID="Content4" ContentPlaceHolderID="PlaceHolderHead" Runat="Server">
    <Swarmops5:ExternalScripts ID="ExternalScripts1" Package="easyui" Control="tree" runat="server" />
    <!-- The Iframe Transport is required for browsers without support for XHR file uploads -->
    <script src="/Scripts/jquery.fileupload/jquery.iframe-transport.js" type="text/javascript" language="javascript"></script>
    <!-- The basic File Upload plugin -->
    <script src="/Scripts/jquery.fileupload/jquery.fileupload.js" type="text/javascript" language="javascript"></script>
	<link rel="stylesheet" type="text/css" href="/Style/v5-easyui-elements.css">

    <script type="text/javascript">

        // The below weird construct circumvents a mono-server optimization bug that kills the string

        var imageFileStart = "\x3Cimg src='";
        var imageFileSuccess = "/Images/Icons/iconshock-invoice-greentick-32px.png";
        var imageFileFail = "/Images/Icons/iconshock-invoice-redcross-32px.png";
        var imageFileEnd = "' /\x3E"; // needs to be separate var rather than inline because of mono-server optimization bug
        var imageUploadSuccess = imageFileStart + imageFileSuccess + imageFileEnd;
        var imageUploadFail = imageFileStart + imageFileFail + imageFileEnd;

        $(document).ready(function () {
            $('#DropBudgets').combotree({
                animate: true,
                height: 30
            });  // Is this init call even necessary?

            $('#DropCostTypes').combotree({
                animate: true,
                height: 30
            });

            $('#SpanBudgets span.combo input.combo-text').click(function () {
                $('#SpanBudgets span.combo span span.combo-arrow').click();
            });
            
            $('#SpanCostTypes span.combo input.combo-text').click(function () {
                $('#SpanCostTypes span.combo span span.combo-arrow').click();
            });


            $('#ButtonUploadHidden').fileupload({
                url: '/Automation/UploadFileHandler.ashx',
                dataType: 'json',
                done: function (e, data) {
                    $.each(data.result, function(index, file) {
                        if (file.error == null || file.error.length < 1) {
                            $('#DivUploadCount').append(imageUploadSuccess);
                        } else {
                            $('#DivUploadCount').append(imageUploadFail); // Assume ERR_NOT_IMAGE
                            alertify.error('File "' + file.name + '" could not be uploaded. Is it really an image?');
                        }

                    });
                    $('#DivProgressUpload').progressbar({ value: 100 });
                    $('#DivProgressUpload').fadeOut('400', function () { $('#DivUploadCount').fadeIn(); });
                },
                progressall: function (e, data) {
                    var progress = parseInt(data.loaded / data.total * 100, 10);
                    $("#DivProgressUpload .progressbar-value").animate(
                        {
                            width: progress + "%"
                        }, { queue: false });
                },
                add: function (e, data) {
                    $('#DivUploadCount').css('display', 'none');
                    $('#DivProgressUpload').progressbar({ value: 0 });  // reset in case of previous uploads on this page
                    $('#DivProgressUpload').fadeIn();
                    data.submit();
                }
            });
            

            $('#ButtonUploadVisible').bind("click" , function () {
                $('#ButtonUploadHidden').click();
            });


            $('#DivProgressUpload').progressbar({ value: 0, max: 100 });
            
        });





        function validateFields() {
            var isValid = true;
            
            isValid = validateTextField('#<%=this.TextAccount.ClientID %>', "<asp:Literal runat="server" ID="LiteralErrorBankAccount" />") && isValid;
            isValid = validateTextField('#<%=this.TextClearing.ClientID %>', "<asp:Literal runat="server" ID="LiteralErrorBankClearing" />") && isValid;
            isValid = validateTextField('#<%=this.TextBank.ClientID %>', "<asp:Literal runat="server" ID="LiteralErrorBankName" />") && isValid;

            if ($('#DropBudgets').combotree('tree').tree('getSelected') == null) {
                isValid = false;
                alertify.error("<asp:Literal runat="server" ID="LiteralErrorBudget" />");
            }

            isValid = validateTextField('#<%=this.TextPurpose.ClientID %>', "<asp:Literal runat="server" ID="LiteralErrorPurpose" />") && isValid;

            $.ajax({
                type: "POST",
                url: "/Automation/FieldValidation.asmx/IsAmountValid",
                data: "{'amount': '" + escape($('#<%=this.TextAmount.ClientID %>').val()) + "'}",
                contentType: "application/json; charset=utf-8",
                dataType: "json",
                async: false,  // blocks until function returns - race conditions otherwise
                success: function (msg) {
                    if (msg.d != true) {
                        isValid = false;
                        alertify.error("<asp:Literal runat="server" ID="LiteralErrorAmount" />");
                        $('#<%=this.TextAmount.ClientID %>').focus();
                    }
                }
            });

            return isValid;
        }
        
        function validateTextField (fieldId, message) {
            if ($(fieldId).val().length == 0) {
                alertify.error(message);
                $(fieldId).focus();
                return false;
            }

            return true;
        }

    </script>
</asp:Content>


<asp:Content ID="Content5" ContentPlaceHolderID="PlaceHolderMain" Runat="Server">
    <div class="entryFields">
        <asp:TextBox runat="server" ID="TextAmount" CssClass="alignRight" />&nbsp;<br/>
        <asp:TextBox runat="server" ID="TextPurpose" />&nbsp;<br/>
        <span id="SpanBudgets"><select class="easyui-combotree" url="Json-ExpensableBudgetsTree.aspx" name="DropBudgets" id="DropBudgets" animate="true" style="width:300px"></select></span>&nbsp;<br/>
        <span id="SpanCostTypes"><select class="easyui-combotree" url="Json-CostTypesTree.aspx" name="DropCostTypes" id="DropCostTypes" animate="true" style="width:300px"></select></span>&nbsp;<br/>
        &nbsp;<br/><!-- placeholder for label-side H2 -->
        
        <!-- file upload begins here -->
        
        <div style="height:36px;float:left"><input id="ButtonUploadVisible" type="button" style="width:36px !important; background-image:url('/Images/Icons/iconshock-diskette-upload-32px.png');background-repeat:no-repeat; background-position:top left"/><input id="ButtonUploadHidden" type="file" name="files[]" multiple style="display:none" /></div><div style="height:36px;width:270px;margin-right:10px;float:right;border:none"><div id="DivUploadCount" style="display:none;overflow:hidden;height:64px"></div><div id="DivProgressUpload" style="width:100%;margin-top:8px;display:none"></div></div>&nbsp;<br/>

        <!-- file upload ends -->

        &nbsp;<br/><!-- placeholder for label-side H2 -->
        <asp:TextBox runat="server" ID="TextBank" />&nbsp;<br/>
        <asp:TextBox runat="server" ID="TextClearing" />&nbsp;<br/>
        <asp:TextBox runat="server" ID="TextAccount" />&nbsp;<br/>
        <asp:Button ID="ButtonRequest" runat="server" CssClass="buttonAccentColor" OnClientClick="return validateFields();" OnClick="ButtonRequest_Click" Text="Request"/>
    </div>
    <div class="entryLabels">
        <asp:Label runat="server" ID="LabelAmount" /><br/>
        <asp:Label runat="server" ID="LabelPurpose" /><br/>
        <asp:Label runat="server" ID="LabelBudget" /><br/>
        <asp:Label runat="server" ID="LabelCostType" /><br/>
        <h2><asp:Label runat="server" ID="LabelHeaderImageFiles" /></h2>
        <asp:Label runat="server" ID="LabelImageFiles" /><br/>
        <h2><asp:Label runat="server" ID="LabelHeaderBankDetails" /></h2>
        <asp:Label runat="server" ID="LabelBankName" /><br/>
        <asp:Label runat="server" ID="LabelBankClearing" /><br/>
        <asp:Label runat="server" ID="LabelBankAccount" />
    </div>
    <div style="clear:both"></div>
</asp:Content>



<asp:Content ID="Content6" ContentPlaceHolderID="PlaceHolderSide" Runat="Server">
</asp:Content>

