import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import '@fontsource/manrope/700.css';
import '@fontsource/inter/400.css';
import '@fontsource/inter/500.css';
import '@fontsource/inter/600.css';
import './styles.css';

const container = document.getElementById('root');

if (container === null) {
  throw new Error('Elemento #root não encontrado.');
}

createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
