import {
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  Output,
  ViewChild
} from '@angular/core';
import { LojaOption } from '../lojas-filtro';

@Component({
  selector: 'app-lojas-multi-dropdown',
  templateUrl: './lojas-multi-dropdown.component.html',
  styleUrl: './lojas-multi-dropdown.component.css'
})
export class LojasMultiDropdownComponent {
  @ViewChild('root', { static: true }) root!: ElementRef<HTMLElement>;

  @Input({ required: true }) lojas: LojaOption[] = [];
  @Input() lojaIdsSelecionadas: string[] = [];
  @Output() lojaIdsSelecionadasChange = new EventEmitter<string[]>();

  /** Aplica classe de erro no gatilho (ex.: validação do formulário). */
  @Input() inputErro = false;

  aberto = false;

  @HostListener('document:click', ['$event'])
  onDocumentClick(ev: MouseEvent): void {
    if (!this.aberto) {
      return;
    }
    const t = ev.target as Node;
    if (this.root.nativeElement.contains(t)) {
      return;
    }
    this.aberto = false;
  }

  get textoResumo(): string {
    if (this.lojas.length === 0) {
      return 'Sem lojas no cadastro';
    }
    const sel = this.lojaIdsSelecionadas ?? [];
    if (sel.length === 0) {
      return 'Selecione lojas…';
    }
    if (sel.length === this.lojas.length) {
      return 'Todas as lojas';
    }
    const nomes = sel
      .map((id) => this.lojas.find((l) => l.id === id)?.nome ?? id)
      .filter(Boolean);
    if (nomes.length === 1) {
      return nomes[0];
    }
    if (nomes.length === 2) {
      return `${nomes[0]}, ${nomes[1]}`;
    }
    return `${nomes[0]}, ${nomes[1]} +${nomes.length - 2}`;
  }

  togglePainel(ev: MouseEvent): void {
    ev.stopPropagation();
    if (this.lojas.length === 0) {
      return;
    }
    this.aberto = !this.aberto;
  }

  estaSelecionada(id: string): boolean {
    return (this.lojaIdsSelecionadas ?? []).includes(id);
  }

  alternar(id: string, ev: Event): void {
    const checked = (ev.target as HTMLInputElement).checked;
    const set = new Set(this.lojaIdsSelecionadas ?? []);
    if (checked) {
      set.add(id);
    } else {
      set.delete(id);
    }
    this.emitir([...set]);
  }

  selecionarTodas(): void {
    this.emitir(this.lojas.map((l) => l.id));
  }

  limparSelecao(): void {
    this.emitir([]);
  }

  private emitir(ids: string[]): void {
    this.lojaIdsSelecionadasChange.emit(ids);
  }
}
