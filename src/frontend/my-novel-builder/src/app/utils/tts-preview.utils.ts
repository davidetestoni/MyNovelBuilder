import { WritingLanguage } from '../types/enums/writing-language';

/** Short localized sample shared by voice previews. */
export function getVoicePreviewText(language: WritingLanguage): string {
    switch (language) {
      case WritingLanguage.Italian:
        return "Ciao, questo è un breve esempio per ascoltare l'anteprima della voce selezionata.";
      case WritingLanguage.French:
        return 'Bonjour, ceci est un court exemple pour prévisualiser la voix sélectionnée.';
      case WritingLanguage.Spanish:
        return 'Hola, este es un ejemplo rápido para previsualizar la voz seleccionada.';
      case WritingLanguage.German:
        return 'Hallo, dies ist ein kurzes Beispiel, um die ausgewählte Stimme vorzuhören.';
      case WritingLanguage.Russian:
        return 'Привет, это короткий пример для предварительного прослушивания выбранного голоса.';
      case WritingLanguage.English:
      default:
        return 'Hello, this is a quick sample to preview the selected voice.';
    }
}
