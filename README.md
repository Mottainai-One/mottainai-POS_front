# Mottainai POS — Front-end

Aplicação desktop para o ponto de venda do Mottainai, desenvolvida para Windows com C#, WPF e .NET. O projeto organiza a experiência do operador entre a identificação no terminal e a tela de vendas.

## Funcionalidades

- Tela de acesso com e-mail e senha, seleção de loja e terminal e opção de exibir ou ocultar a senha.
- Navegação entre a tela de acesso e a tela de venda, com identificação da loja e informações da operação.
- Campo de código de barras para localizar um produto e apresentar nome, código e SKU.
- Modelos de domínio para produtos, lojas, funcionários, terminais, turnos, vendas, itens, pagamentos e documentos fiscais.

O aplicativo usa serviços locais para as informações apresentadas nas telas. Os modelos representam dados usados pelo domínio; sua presença não implica que todas as operações correspondentes estejam disponíveis na interface. A API do PDV é mantida separadamente no [Mottainai POS Back-end](https://github.com/Mottainai-One/Mottainai-POS_back); a integração entre os dois componentes deve ser verificada na versão utilizada.

## Tecnologias

- C# e .NET 10 para Windows.
- WPF e XAML para janelas, páginas e recursos visuais.
- GitHub Actions para restauração e compilação da solução em Windows.

## Estrutura

```text
.
├── PDVMottainai.slnx
├── PDVMottainai/
│   ├── App.xaml               # inicialização da aplicação
│   ├── MainWindow.xaml        # janela e navegação
│   ├── Views/                 # telas de acesso e venda
│   ├── Models/                # modelos do domínio
│   ├── Services/              # serviços usados pelas telas
│   ├── IMG/                   # recursos visuais
│   └── PDVMottainai.csproj
├── .github/workflows/         # build no GitHub Actions
└── LICENSE
```

## Requisitos e execução

Instale o Windows e o SDK .NET 10. Na raiz do repositório, execute:

```powershell
dotnet restore PDVMottainai.slnx
dotnet build PDVMottainai.slnx
dotnet run --project PDVMottainai/PDVMottainai.csproj
```

O workflow [`.NET Desktop CI`](.github/workflows/dotnet-desktop.yml) restaura e compila a solução nas configurações Debug e Release. Os comandos e a conexão com serviços externos dependem da configuração da máquina e da versão utilizada.

## Projetos relacionados

- [API local do PDV](https://github.com/Mottainai-One/Mottainai-POS_back)
- [Documentação do ecossistema](https://github.com/Mottainai-One/Mottainai-Hub)
- [Licença](LICENSE)
