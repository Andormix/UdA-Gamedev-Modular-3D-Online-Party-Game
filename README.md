
# Rush Hour: Architecting a Modular 3D Online Party Game

> **Treball Final de Grau en Enginyeria Informàtica**  
> **Universitat d'Andorra (UdA) | Curs 2025-2026**
> **Autor:** Eric Torrontera Ruiz  
> **Tutors:** Jan Sau Batlle & Josep Ribó Ferriz

---

### 🎬 Gameplay / Demostració en vídeo

[![Rush Hour Demo](https://img.youtube.com/vi/placeholder/maxresdefault.jpg)](https://drive.google.com/drive/folders/1LWDcCUO_mqptqqfOhNbFuWpDe6j5OyAp)

> ℹ️ *Fes clic a la imatge per veure el vídeo de demostració del projecte a Google Drive.*

##  Nota sobre l'accés al repositori i Drets d'Autor (Copyright)

Per motius de complir amb les llicències comercials i els termes d'ús de la **Unity Asset Store (EULA)** i d'altres paquets de pagament utilitzats, **els fitxers font i els recursos multimèdia d'aquest projecte es mantenen en un repositori privat**. 

Aquest repositori públic actua com a **demostració tècnica i documentació de l'arquitectura de software** dissenyada per al projecte de grau. Si ets un reclutador o avaluador tècnic i vols consultar el codi o una demo executable (Vertical Slice), pots posar-te en contacte amb mi directament.

---

## Resum del Projecte

**Rush Hour** és un videojoc *Party Game* de gestió 3D ambientat en una cafeteria d'Andorra. El jugador (o jugadors) ha d'atendre els clients, preparar comandes, gestionar el cobrament mitjançant un TPV virtual i mantenir les taules netes sota la pressió del temps.

El projecte s'ha concebut amb una **arquitectura híbrida i modular** (*Coop-First*), dissenyada des del primer moment per ser fàcilment escalable en contingut (mapes, mecaniques i modes de joc) sense haver de refactoritzar el nucli del sistema.

<table>
  <tr>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/a61d918e-204a-4e48-a2b3-1634a18b6114" width="100%" alt="Diapositiva 1" />
    </td>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/ee486c7d-125f-4685-a181-fba04cd4cf9d" width="100%" alt="Diapositiva 2" />
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/a3271c59-aca1-4ca7-9e3a-fd8fb045bfe9" width="100%" alt="Diapositiva 3" />
    </td>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/060a9946-a9fc-4286-a5ff-45f6b8551ddc" width="100%" alt="Diapositiva 4" />
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/35a4cda8-671f-4b05-a181-7f85bfb523c7" width="100%" alt="Diapositiva 5" />
    </td>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/92ecc19c-67be-4e6c-91d6-9ecb6b606f61" width="100%" alt="Diapositiva 6" />
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/18dd8fcf-1969-4e6e-a337-ca9d548b7ba0" width="100%" alt="Diapositiva 7" />
    </td>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/5bdd4181-c618-4ff9-9c89-2917fea03d19" width="100%" alt="Diapositiva 8" />
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/bf16fa84-f591-47f5-ba9d-b08971fb5f0b" width="100%" alt="Diapositiva 9" />
    </td>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/d5131cda-b3bd-41bf-aeec-f5f2781f7f4e" width="100%" alt="Diapositiva 10" />
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/4f39139b-bdc0-4bee-a95a-106aaaa28f05" width="100%" alt="Diapositiva 11" />
    </td>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/453a09ef-0114-47cf-8c3a-e5a85eac61cb" width="100%" alt="Diapositiva 12" />
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/ba2def99-ad51-4f5f-9186-2cd838d4fc0c" width="100%" alt="Diapositiva 13" />
    </td>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/1f6d251f-5200-42df-9caf-e08838ca42d9" width="100%" alt="Diapositiva 14" />
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/f1a02167-67dd-417c-b42d-0a43d384292a" width="100%" alt="Diapositiva 15" />
    </td>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/bb1009a7-3dfc-4b4f-a6ae-0718eecfc1d0" width="100%" alt="Diapositiva 16" />
    </td>
  </tr>
</table>
