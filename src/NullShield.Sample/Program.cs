using System;
using NullShield.Core.Attributes;
using NullShield.Core.Enums;

namespace NullShield.Sample
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== NullShield Güvenlik Duvarı Testi ===");

            try
            {
                // 1. Test: Geçerli veri gönderiyoruz (Hata vermemeli)
                YullaniciKaydet("Talha Şadar", "talha@example.com");
                Console.WriteLine("1. Test Başarılı: Geçerli kullanıcı kaydedildi.\n");

                // 2. Test: Null veri gönderiyoruz (Source Generator yakalamalı!)
                Console.WriteLine("2. Test Başlatılıyor: Null isim gönderiliyor...");
                YullaniciKaydet(null!, "test@example.com");
            }
            catch (ArgumentNullException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[KORUMA TETİKLENDİ] Yakalanan Hata: {ex.Message}");
                Console.ResetColor();
            }

            Console.ReadLine();
        }

        // Metodun tepesine imzamızı atıyoruz
        [NullShield(MitigationStrategy.ThrowException)]
        public static void YullaniciKaydet(string username, string email)
        {
            // Biz buraya manuel olarak "if(username == null)" yazmadık!
            // Bizim jeneratör derleme anında otomatik enjekte edecek.
            
            // Arka planda üretilen guard metodunu çağırıyoruz:
            NullShield_Guard_Program_YullaniciKaydet.ValidateParameters(username, email);

            Console.WriteLine($"[Metod İçi] Veritabanı işlemi yapılıyor: {username} ({email})");
        }
    }
}