/** Convierte un objeto { valor: etiqueta } en opciones de <select>. */
export const toOptions = (labels: Record<string, string>) => Object.entries(labels).map(([value, label]) => ({ value, label }))
