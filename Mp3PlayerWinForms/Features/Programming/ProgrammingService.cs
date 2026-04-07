    // CLASSE: ProgrammingService
// VERSÃO: 1.0
// DATA: 2026-04-03
// MOTIVO: Implementação do serviço de lógica para processar as regras de tempo e periodicidade.

using System;
using System.Collections.Generic;
using System.Linq;
using XP3.Models;

namespace XP3.Features.Programming
{
    public class ProgrammingService
    {
        // METODO: ObterPeriodicidadesAtuais
        // VERSÃO: 1.0
        // MOTIVO: Retorna a lista de códigos de periodicidade válidos para o dia atual da semana.
        // Regras: 1=Diário, 2=Dias Úteis, 3=Sábado, 4=Domingo.
        public List<int> ObterPeriodicidadesAtuais()
        {
            DayOfWeek hoje = DateTime.Now.DayOfWeek;
            var idsValidos = new List<int>();

            // O código 1 (Diário) é sempre válido, independente do dia
            idsValidos.Add(1);

            switch (hoje)
            {
                case DayOfWeek.Sunday:
                    idsValidos.Add(4); // Domingo
                    break;

                case DayOfWeek.Saturday:
                    idsValidos.Add(3); // Sábado
                    break;

                default:
                    // De segunda a sexta
                    idsValidos.Add(2); // Dias Úteis
                    break;
            }

            return idsValidos;
        }

        // METODO: SugerirPlaylistPorHorario
        // VERSÃO: 1.0
        // MOTIVO: Filtra a programação baseada na hora atual e periodicidade para dizer qual lista deveria tocar.
        public int? SugerirPlaylistPorHorario(List<ProgramacaoModel> todasProgramacoes)
        {
            if (todasProgramacoes == null || todasProgramacoes.Count == 0) return null;

            var agora = DateTime.Now;
            var horaAtual = agora.TimeOfDay;

            // Função interna para filtrar as programações que são válidas para um dia específico
            List<ProgramacaoModel> ObterValidas(DayOfWeek dia)
            {
                return todasProgramacoes.Where(p =>
                    p.Periodicidade == 1 || // 1: Todos os dias
                    (p.Periodicidade == 2 && dia >= DayOfWeek.Monday && dia <= DayOfWeek.Friday) || // 2: Dias de semana
                    (p.Periodicidade == 3 && dia == DayOfWeek.Saturday) || // 3: Sábados
                    (p.Periodicidade == 4 && dia == DayOfWeek.Sunday)      // 4: Domingos
                ).ToList();
            }

            // 1. Busca as programações válidas para HOJE
            var validasHoje = ObterValidas(agora.DayOfWeek);

            // 2. Tenta achar a mais recente de HOJE que o horário já passou
            var progAtual = validasHoje
                .Where(p => p.HorarioInicio.TimeOfDay <= horaAtual)
                .OrderByDescending(p => p.HorarioInicio.TimeOfDay)
                .FirstOrDefault();

            if (progAtual != null)
                return progAtual.PlaylistId;

            // 3. TRAVESSIA DA MADRUGADA: Se não achou nenhuma hoje (ex: são 02:00 AM e a primeira é 06:00 AM),
            // ele busca a última programação válida que rodou ONTEM à noite.
            var validasOntem = ObterValidas(agora.AddDays(-1).DayOfWeek);
            var progOntem = validasOntem
                .OrderByDescending(p => p.HorarioInicio.TimeOfDay)
                .FirstOrDefault();

            return progOntem?.PlaylistId;
        }

    }
}