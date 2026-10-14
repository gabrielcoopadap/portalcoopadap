import { Component, ElementRef, EventEmitter, Input, NgZone, OnChanges, Output, SimpleChanges, ViewChild, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AgroCertApi } from '../../api/agrocert-api.service';
import { Especie, RegistroBio } from '../../api/modelos';
import { ModalComponent } from '../../compartilhado/modal.component';
import { AvisosService } from '../../compartilhado/avisos';
import { RotaPipe } from '../../util/pipes';
import { AREAS_BIO, ORIGENS_REGISTRO, rotuloOrigem, rotuloReino } from '../../util/rotulos';
import { PropriedadeItem } from './modelos';

const dois = (n: number) => String(n).padStart(2, '0');
function dataHoraLocal(d: Date): string {
  return `${d.getFullYear()}-${dois(d.getMonth() + 1)}-${dois(d.getDate())}T${dois(d.getHours())}:${dois(d.getMinutes())}`;
}

interface FormRegistro {
  especieId: string;
  dataHora: string;
  origem: string;
  camera: string;
  area: string;
  propriedadeId: string;
  latitude: string;
  longitude: string;
  quantidade: number | null;
  observacao: string;
}

@Component({
  selector: 'ac-ff-modal-registro',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, RouterLink, ModalComponent, RotaPipe],
  templateUrl: './modal-registro.component.html',
})
export class ModalRegistroComponent implements OnChanges {
  private readonly api = inject(AgroCertApi);
  private readonly avisos = inject(AvisosService);
  private readonly zona = inject(NgZone);

  @Input() especies: Especie[] = [];
  @Input() propriedades: PropriedadeItem[] = [];
  @Output() salvo = new EventEmitter<RegistroBio>();

  @ViewChild('foto') fotoInput?: ElementRef<HTMLInputElement>;

  aberto = false;
  salvando = false;
  localizando = false;
  arquivo: File | null = null;
  maxDataHora = '';
  form: FormRegistro = this.novoForm();
  gruposEspecies: { rotulo: string; especies: Especie[] }[] = [];

  readonly origens = ORIGENS_REGISTRO;
  readonly areas = AREAS_BIO;
  readonly rotuloOrigem = rotuloOrigem;

  ngOnChanges(m: SimpleChanges): void {
    if (m['especies']) {
      this.gruposEspecies = ['FAUNA', 'FLORA'].map(r => ({
        rotulo: rotuloReino(r),
        especies: (this.especies ?? []).filter(e => e.reino === r)
          .sort((a, b) => a.nomePopular.localeCompare(b.nomePopular, 'pt-BR')),
      }));
    }
    if (m['propriedades'] && !this.form.propriedadeId) this.form.propriedadeId = this.propriedadePadrao();
  }

  abrir(latitude?: string, longitude?: string): void {
    if (latitude !== undefined && longitude !== undefined) {
      this.form.latitude = latitude;
      this.form.longitude = longitude;
    }
    this.maxDataHora = dataHoraLocal(new Date(Date.now() + 5 * 60_000));
    this.aberto = true;
  }

  minhaLocalizacao(): void {
    if (!navigator.geolocation) return;
    this.localizando = true;
    navigator.geolocation.getCurrentPosition(pos => this.zona.run(() => {
      this.form.latitude = pos.coords.latitude.toFixed(6);
      this.form.longitude = pos.coords.longitude.toFixed(6);
      this.localizando = false;
    }), () => this.zona.run(() => {
      this.localizando = false;
      alert('Não foi possível obter a localização. Verifique a permissão do navegador.');
    }), { enableHighAccuracy: true, timeout: 15000 });
  }

  salvar(): void {
    if (this.salvando) return;
    const f = this.form;
    const fd = new FormData();
    const add = (k: string, v: string | number | null | undefined) => {
      if (v !== null && v !== undefined && String(v) !== '') fd.append(k, String(v));
    };
    add('EspecieId', f.especieId);
    add('DataHora', f.dataHora);
    add('Origem', f.origem);
    if (f.origem === 'CAMERA') add('Camera', f.camera);
    add('Area', f.area);
    add('PropriedadeId', f.propriedadeId);
    add('Latitude', f.latitude?.trim());
    add('Longitude', f.longitude?.trim());
    add('Quantidade', f.quantidade);
    add('Observacao', f.observacao);
    if (this.arquivo) fd.append('Foto', this.arquivo, this.arquivo.name);

    this.salvando = true;
    this.api.criarRegistro(fd).subscribe({
      next: r => {
        this.salvando = false;
        this.avisos.ok(`Ocorrência de ${r.especie} registrada.`);
        this.aberto = false;
        this.form = this.novoForm();
        this.arquivo = null;
        if (this.fotoInput) this.fotoInput.nativeElement.value = '';
        this.salvo.emit(r);
      },
      error: e => { this.salvando = false; this.avisos.erro(e); },
    });
  }

  private propriedadePadrao(): string {
    return this.propriedades?.length === 1 ? String(this.propriedades[0].id) : '';
  }

  private novoForm(): FormRegistro {
    return {
      especieId: '', dataHora: dataHoraLocal(new Date()), origem: 'AVISTAMENTO', camera: '', area: AREAS_BIO[0],
      propriedadeId: this.propriedadePadrao(), latitude: '', longitude: '', quantidade: 1, observacao: '',
    };
  }
}
