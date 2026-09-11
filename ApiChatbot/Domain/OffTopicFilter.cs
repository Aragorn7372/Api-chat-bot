namespace ApiChatbot.Domain;

public class OffTopicFilter
{
    private readonly string _name;
    private readonly string _rejectionMessage;

    private static readonly string[][] OffTopicKeywords =
    [
        ["fútbol", "baloncesto", "tenis", "champions", "liga", "mundial", "partido", "jugador", "deporte", "copa", "campeonato", "gol", "estadio", "entrenador", "árbitro", "selección", "nba", "nfl"],
        ["gobierno", "presidente", "partido político", "elecciones", "voto", "política", "ministro", "senado", "congreso", "alcalde", "diputado"],
        ["película", "serie", "música", "cantante", "actor", "famoso", "celebridad", "videojuego", "pokemon", "netflix", "disney", "spotify", "canción", "álbum", "cine", "tv"],
        ["perro", "gato", "animal", "mascota", "caballo", "pez", "pájaro", "charmander", "pokémon", "dinosaurio", "tortuga"],
        ["receta", "cocina", "plato", "ingrediente", "restaurante", "comida", "bebida", "cocinar", "chef", "cena", "desayuno"],
        ["enfermedad", "medicina", "doctor", "hospital", "salud", "tratamiento", "síntoma", "paciente", "cirugía", "virus", "bacteria"],
        ["dios", "iglesia", "religión", "creencia", "fe", "rezar", "oración", "biblia", "cristo", "virgen"],
        ["clima", "lluvia", "nieve", "tormenta", "terremoto", "huracán", "temperatura", "pronóstico", "meteorología"],
        ["receta", "moda", "ropa", "zapatos", "maquillaje", "belleza", "corte", "peinado"],
        ["acción", "aventura", "rol", "estrategia", "simulación", "multijugador", "nintendo", "playstation", "xbox", "steam"],
        ["youtube", "tiktok", "instagram", "twitter", "influencer", "streamer", "viral"],
    ];

    public OffTopicFilter(string name)
    {
        _name = name;
        _rejectionMessage = $"Lo siento, solo puedo brindar información sobre el portafolio de {_name}. Si tienes preguntas sobre su formación, proyectos, habilidades técnicas o experiencia laboral, estaré encantado de ayudarte.";
    }

    public bool IsOffTopic(string message)
    {
        var lower = message.ToLowerInvariant();
        return OffTopicKeywords.Any(category => category.Any(keyword => lower.Contains(keyword)));
    }

    public string RejectionMessage => _rejectionMessage;
}
