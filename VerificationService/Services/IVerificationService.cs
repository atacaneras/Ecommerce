// VerificationService/Services/IVerificationService.cs

using VerificationService.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VerificationService.Services
{
    public interface IVerificationService
    {
        /// <summary>
        /// Siparişi onaylar, stok düşüm mesajını yayımlar ve log kaydını günceller.
        /// </summary>
        /// <param name="orderId">Onaylanacak sipariş ID'si.</param>
        /// <param name="approvedBy">Onaylayan kullanıcı/sistem bilgisi.</param>
        /// <returns>Güncellenmiş doğrulama logu (VerificationResponse) veya null.</returns>
        Task<VerificationResponse?> ApproveOrderAsync(Guid orderId, string? approvedBy);

        /// <summary>
        /// Siparişi reddeder, stok rezervasyonunu iptal eder ve log kaydını günceller.
        /// </summary>
        /// <param name="orderId">Reddedilecek sipariş ID'si.</param>
        /// <param name="reason">Ret sebebi.</param>
        /// <returns>Güncellenmiş doğrulama logu (VerificationResponse) veya null.</returns>
        Task<VerificationResponse?> RejectOrderAsync(Guid orderId, string reason);

        /// <summary>
        /// Tüm bekleyen (Requested) onay taleplerini getirir.
        /// </summary>
        Task<IEnumerable<VerificationResponse>> GetPendingVerificationsAsync();

        /// <summary>
        /// Tüm onay taleplerini (Approved, Rejected, Failed, Requested) getirir.
        /// </summary>
        Task<IEnumerable<VerificationResponse>> GetAllVerificationsAsync();

        /// <summary>
        /// Belirli bir siparişin onay talebi logunu getirir.
        /// </summary>
        Task<VerificationResponse?> GetVerificationByOrderIdAsync(Guid orderId);
    }
}