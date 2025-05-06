﻿using BaraoFeedback.Application.DTOs.Shared;
using BaraoFeedback.Application.DTOs.Ticket;
using BaraoFeedback.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace BaraoFeedback.Application.Services.Email;

public class EmailService : IEmailService
{
    private readonly EmailSenderOptions _options;
    private readonly UserManager<ApplicationUser> _userManager;

    public EmailService(IOptions<EmailSenderOptions> options, UserManager<ApplicationUser> userManager)
    {
        _options = options.Value;
        _userManager = userManager;
    }

    private SmtpClient ObterClient()
    {
        return new SmtpClient("smtp.office365.com", 587)
        {
            Credentials = new System.Net.NetworkCredential("fabricadesoftware@baraodemaua.edu.br", "=Eup@5b+"),
            EnableSsl = true
        };
    }

    #region Métodos Públicos

    public async Task<BaseResponse<bool>> SendPassword(string email, string userName, string password)
    {
        string subject = "Conta criada com sucesso!";
        string body = EmailTemplates.GeneratePasswordEmailBody(userName, password);
        return await EnviarEmail(email, subject, body);
    }

    public async Task<BaseResponse<bool>> SendConfirmMail(string email, string userName, string confirmationLink)
    {
        string subject = "Confirme seu email";
        string body = EmailTemplates.GenerateConfirmationEmailBody(userName, confirmationLink);
        return await EnviarEmail(email, subject, body);
    }

    public async Task<BaseResponse<bool>> SendForgotPasswordEmail(string email, string userName, string password)
    {
        string subject = "Redefinição de Senha - Barao Feedback";
        string body = EmailTemplates.GenerateForgotPasswordEmailBody(userName, password);
        return await EnviarEmail(email, subject, body);
    }

    public async Task<BaseResponse<bool>> SendEmail(TicketResponse ticket)
    {
        var destinatarios = _userManager.Users
            .Where(x => x.Type == "admin")
            .Select(x => x.Email)
            .Where(IsValidEmail)
            .ToArray();

        var response = new BaseResponse<bool>();
        if (!destinatarios.Any())
        {
            response.Errors.AddError("Nenhum administrador com e-mail válido encontrado.");
            return response;
        }

        string subject = $"Novo chamado #{ticket.TicketId}";
        string body = EmailTemplates.GenerateTicketEmailBody(ticket);
        return await EnviarEmail(destinatarios, subject, body);
    }

    #endregion

    #region Auxiliares

    private async Task<BaseResponse<bool>> EnviarEmail(string destinatario, string assunto, string corpo)
    {
        return await EnviarEmail(new[] { destinatario }, assunto, corpo);
    }

    private async Task<BaseResponse<bool>> EnviarEmail(string[] destinatarios, string assunto, string corpo)
    {
        var response = new BaseResponse<bool>();

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress("fabricadesoftware@baraodemaua.edu.br"),
                Subject = assunto,
                Body = corpo,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8
            };

            foreach (var destinatario in destinatarios.Distinct())
            {
                if (IsValidEmail(destinatario))
                    message.To.Add(destinatario.Trim());
            }

            using var client = ObterClient();
            client.Send(message);
        }
        catch (SmtpFailedRecipientsException ex)
        {
            var invalidRecipients = ex.InnerExceptions
                .OfType<SmtpFailedRecipientException>()
                .Select(e => e.FailedRecipient)
                .Distinct();

            response.Errors.AddError($"Email(s) inválido(s): {string.Join(", ", invalidRecipients)}");
        }
        catch (Exception ex)
        {
            response.Errors.AddError($"Erro ao enviar e-mail: {ex.Message}");
        }

        return response;
    }

    private bool IsValidEmail(string email)
    {
        var pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email, pattern);
    }

    #endregion
}

public static class EmailTemplates
{
    public static string GeneratePasswordEmailBody(string userName, string password) => $@"
        <html>
            <body style=""font-family: Arial, sans-serif; color: #333;"">
                <h2>Conta de Administrador Criada com Sucesso</h2>
                <p>Olá {userName},</p>
                <p>Sua conta de administrador foi criada com sucesso.</p>
                <p><strong>Usuário:</strong> {userName}<br/><strong>Senha:</strong> {password}</p>
                <p>Recomendamos alterar sua senha no primeiro acesso.</p>
                <p>Atenciosamente,<br/>Equipe de Suporte</p>
            </body>
        </html>";

    public static string GenerateConfirmationEmailBody(string userName, string confirmationLink) => $@"
        <html>
            <body style='font-family: Arial, sans-serif; background-color: #f9f9f9; padding: 20px;'>
                <div style='max-width: 600px; margin: auto; background-color: #ffffff; padding: 30px; border-radius: 10px; box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);'>
                    <h2>Olá, {userName}!</h2>
                    <p>Para ativar sua conta, clique no botão abaixo:</p>
                    <a href='{confirmationLink}' style='padding: 10px 20px; background-color: #007bff; color: #fff; text-decoration: none; border-radius: 5px;'>Confirmar E-mail</a>
                    <p>Ou copie e cole o link no navegador:</p>
                    <p><a href='{confirmationLink}'>{confirmationLink}</a></p>
                    <p>Se você não criou essa conta, ignore este e-mail.</p>
                </div>
            </body>
        </html>";

    public static string GenerateForgotPasswordEmailBody(string userName, string newPassword) => $@"
    <html>
        <body style='font-family: Arial, sans-serif; background-color: #f9f9f9; padding: 20px;'>
            <div style='max-width: 600px; margin: auto; background-color: #ffffff; padding: 30px; border-radius: 10px; box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);'>
                <h2>Olá, {userName}!</h2>
                <p>Recebemos uma solicitação para redefinir sua senha.</p>
                <p><strong>Sua nova senha:</strong></p>
                <div style='background-color: #f1f1f1; padding: 10px 15px; border-radius: 5px; font-weight: bold; margin: 10px 0; font-size: 16px;'>
                    {newPassword}
                </div>
                <p>Você pode usar essa senha para acessar sua conta e, se desejar, alterá-la nas configurações do seu perfil.</p>
                <a href='https://barao-tickets-ui.vercel.app/login' style='display: inline-block; margin-top: 20px; padding: 12px 24px; background-color: #007bff; color: white; text-decoration: none; border-radius: 6px; font-weight: bold;'>Acessar Plataforma</a>
                <p style='margin-top: 30px; font-size: 12px; color: #999;'>Se você não solicitou essa redefinição, pode ignorar este e-mail.</p>
            </div>
        </body>
    </html>";


    public static string GenerateTicketEmailBody(TicketResponse ticket) => $@"
        <html>
            <body style='font-family: Arial, sans-serif;'>
                <h2 style='color: #2e6c80;'>Novo Chamado Recebido</h2>
                <p><strong>ID do Chamado:</strong> {ticket.TicketId}</p>
                <p><strong>Descrição:</strong><br />{ticket.Description}</p>
                <hr />
                <p><strong>Aluno:</strong> {ticket.Title}</p>
                <p><strong>Instituição:</strong> {ticket.InstitutionName}</p>
                <p><strong>Local:</strong> {ticket.LocationName}</p>
                <p><strong>Categoria:</strong> {ticket.CategoryName}</p>
                <p><strong>Data de Abertura:</strong> {ticket.CreatedAt}</p>
            </body>
        </html>";
}

public class EmailSenderOptions
{
    public string FromName { get; set; }
    public string FromEmail { get; set; }
    public string Host { get; set; }
    public int Port { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
}
