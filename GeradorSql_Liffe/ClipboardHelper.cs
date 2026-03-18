using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace GeradorSql_Liffe.Utils
{
    public static class ClipboardHelper
    {
        public static void CopiarComRetentativa(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return;

            for (int i = 0; i < 10; i++) // Aumentamos para 10 tentativas
            {
                try
                {
                    Clipboard.SetText(texto);
                    return;
                }
                catch (COMException ex)
                {
                    // Erro 0x800401D0 é o CLIPBRD_E_CANT_OPEN
                    if ((uint)ex.ErrorCode != 0x800401D0) throw;
                    Thread.Sleep(100); // Espera 100ms
                }
            }

            MessageBox.Show("O Windows impediu o acesso à área de transferência. Tente fechar programas de captura de tela ou o histórico do Clipboard (Win+V).", "Erro de Sistema");
        }
    }
}