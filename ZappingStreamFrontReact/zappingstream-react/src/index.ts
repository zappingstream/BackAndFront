export const removeDiacritics = (text: string) => {
    if (!text) return text;
    return text.normalize("NFD").replace(/[\u0300-\u036f]/g, "");
};

export const getFreshImage = (url: string, lastActivityAt: string) => {
    if (!url) return url;
    const cacheBuster = lastActivityAt ? encodeURIComponent(lastActivityAt) : Date.now().toString();
    const connector = url.includes("?") ? "&" : "?";
    return `${url}${connector}act=${cacheBuster}`;
};

export const formatActivityDate = (isoString: string) => {
    if (!isoString) return "Sin actividad registrada";
    const date = new Date(isoString);
    if (date.getFullYear() <= 2000) return "Sin actividad registrada";

    const diffDays = (Date.now() - date.getTime()) / (1000 * 60 * 60 * 24);
    if (diffDays > 365) return "Sin actividad registrada";

    return `Última Actividad: ${date.toLocaleDateString('es-AR')} - ${date.toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit', hour12: false})}`;
};