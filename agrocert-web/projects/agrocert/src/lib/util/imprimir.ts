export function imprimirRelatorio(): void {
  document.body.classList.add('ac-imprimindo');
  const limpar = () => {
    document.body.classList.remove('ac-imprimindo');
    window.removeEventListener('afterprint', limpar);
  };
  window.addEventListener('afterprint', limpar);
  window.print();
  setTimeout(limpar, 1000);
}
